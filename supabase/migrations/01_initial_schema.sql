-- Migration 01_initial_schema.sql
-- VNU Focus: Pomodoro - Core Database Schema & RPC Functions

-- Kích hoạt extension pgcrypto để sinh UUID nếu cần
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================================
-- 1. BẢNG PROFILES (Hồ sơ sinh viên)
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.profiles (
    id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    student_name VARCHAR(100) NOT NULL DEFAULT 'Sinh viên VNU',
    training_points INTEGER NOT NULL DEFAULT 0 CHECK (training_points >= 0),
    avatar_id VARCHAR(50) NOT NULL DEFAULT 'default_student',
    created_at TIMESTAMPTZ NOT NULL DEFAULT timezone('utc'::text, now()),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT timezone('utc'::text, now())
);

-- Bật Row Level Security (RLS) cho bảng profiles
ALTER TABLE public.profiles ENABLE ROW LEVEL SECURITY;

-- Policies cho profiles
CREATE POLICY "Mọi người có thể đọc profile" 
    ON public.profiles FOR SELECT 
    USING (true);

CREATE POLICY "Chỉ người sở hữu mới được cập nhật profile của mình" 
    ON public.profiles FOR UPDATE 
    USING (auth.uid() = id);

CREATE POLICY "Người dùng có thể tạo profile của mình" 
    ON public.profiles FOR INSERT 
    WITH CHECK (auth.uid() = id);

-- Trigger tự động tạo hồ sơ profile khi người dùng đăng ký hoặc đăng nhập ẩn danh
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER SET search_path = public
AS $$
BEGIN
    INSERT INTO public.profiles (id, student_name, avatar_id)
    VALUES (
        new.id,
        COALESCE(new.raw_user_meta_data->>'student_name', 'Sinh viên VNU'),
        COALESCE(new.raw_user_meta_data->>'avatar_id', 'default_student')
    )
    ON CONFLICT (id) DO NOTHING;
    RETURN new;
END;
$$;

DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();

-- ============================================================================
-- 2. BẢNG FOCUS_SESSIONS (Lịch sử phiên học)
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.focus_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
    duration_minutes INTEGER NOT NULL CHECK (duration_minutes >= 1 AND duration_minutes <= 90),
    room_type VARCHAR(20) NOT NULL CHECK (room_type IN ('dorm', 'library')),
    started_at TIMESTAMPTZ NOT NULL DEFAULT timezone('utc'::text, now()),
    completed_at TIMESTAMPTZ,
    status VARCHAR(20) NOT NULL DEFAULT 'in_progress' CHECK (status IN ('in_progress', 'completed', 'cancelled')),
    reward_points INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT timezone('utc'::text, now())
);

-- Chỉ mục tối ưu tra cứu phiên học theo người dùng và trạng thái
CREATE INDEX IF NOT EXISTS idx_focus_sessions_user_status 
    ON public.focus_sessions(user_id, status);

-- Bật Row Level Security (RLS) cho bảng focus_sessions
ALTER TABLE public.focus_sessions ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Người dùng chỉ đọc phiên học của mình" 
    ON public.focus_sessions FOR SELECT 
    USING (auth.uid() = user_id);

CREATE POLICY "Người dùng tạo phiên học của mình" 
    ON public.focus_sessions FOR INSERT 
    WITH CHECK (auth.uid() = user_id);

-- ============================================================================
-- 3. STORED PROCEDURES / RPC CHỐNG GIAN LẬN
-- ============================================================================

-- 3.1. Bắt đầu phiên học: start_focus_session
CREATE OR REPLACE FUNCTION public.start_focus_session(
    p_duration_minutes INTEGER,
    p_room_type VARCHAR
)
RETURNS UUID
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_user_id UUID;
    v_session_id UUID;
BEGIN
    v_user_id := auth.uid();
    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'Người dùng chưa được xác thực.';
    END IF;

    -- Đảm bảo thời lượng hợp lệ từ 1 đến 90 phút
    IF p_duration_minutes < 1 OR p_duration_minutes > 90 THEN
        RAISE EXCEPTION 'Thời gian học phải từ 1 đến 90 phút.';
    END IF;

    -- Đảm bảo room_type hợp lệ
    IF p_room_type NOT IN ('dorm', 'library') THEN
        RAISE EXCEPTION 'Loại phòng không hợp lệ. Chỉ chấp nhận dorm hoặc library.';
    END IF;

    -- Đảm bảo profile tồn tại trước khi tạo session
    INSERT INTO public.profiles (id)
    VALUES (v_user_id)
    ON CONFLICT (id) DO NOTHING;

    -- Hủy các session dang dở trước đó của user (nếu có)
    UPDATE public.focus_sessions
    SET status = 'cancelled'
    WHERE user_id = v_user_id AND status = 'in_progress';

    -- Tạo session mới với started_at lấy từ đồng hồ server
    INSERT INTO public.focus_sessions (user_id, duration_minutes, room_type, started_at, status)
    VALUES (v_user_id, p_duration_minutes, p_room_type, now(), 'in_progress')
    RETURNING id INTO v_session_id;

    RETURN v_session_id;
END;
$$;

-- 3.2. Nghiệm thu & Nhận điểm rèn luyện: claim_focus_reward
CREATE OR REPLACE FUNCTION public.claim_focus_reward(
    p_session_id UUID
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_user_id UUID;
    v_session RECORD;
    v_elapsed_seconds NUMERIC;
    v_points_earned INTEGER;
    v_new_total INTEGER;
BEGIN
    v_user_id := auth.uid();
    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'Người dùng chưa được xác thực.';
    END IF;

    -- Lấy thông tin session đang chạy của đúng user
    SELECT * INTO v_session
    FROM public.focus_sessions
    WHERE id = p_session_id AND user_id = v_user_id AND status = 'in_progress';

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Phiên học không tồn tại hoặc đã được xử lý.';
    END IF;

    -- Tính số giây thực tế đã trôi qua dựa theo đồng hồ Server
    v_elapsed_seconds := EXTRACT(EPOCH FROM (now() - v_session.started_at));

    -- Cho phép độ trễ mạng tối đa 30 giây
    IF v_elapsed_seconds < (v_session.duration_minutes * 60 - 30) THEN
        RAISE EXCEPTION 'Chưa đủ thời gian tập trung để nhận điểm.';
    END IF;

    -- Công thức tính Điểm rèn luyện: 1 điểm cho mỗi phút tập trung
    v_points_earned := v_session.duration_minutes;

    -- Cập nhật trạng thái session thành completed trong cùng transaction
    UPDATE public.focus_sessions
    SET status = 'completed',
        completed_at = now(),
        reward_points = v_points_earned
    WHERE id = p_session_id;

    -- Cộng điểm rèn luyện vào hồ sơ sinh viên
    UPDATE public.profiles
    SET training_points = training_points + v_points_earned,
        updated_at = now()
    WHERE id = v_user_id
    RETURNING training_points INTO v_new_total;

    RETURN jsonb_build_object(
        'success', true,
        'points_earned', v_points_earned,
        'new_total_points', v_new_total
    );
END;
$$;
