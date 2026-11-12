namespace StudyRoom.Application.Messages;

public sealed record FlashcardPromptMessage(string CardId, string Question, string Answer, int TimeoutSeconds = 30);
