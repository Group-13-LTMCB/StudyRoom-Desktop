# StudyRoom Backend (AWS Lambda .NET 8)

This directory contains the serverless backend for StudyRoom, deployed as AWS Lambda functions running on .NET 8.

## Structure (planned)
```
backend/
├── StudyRoom.Lambda/           # Lambda function handlers
│   ├── Functions/
│   │   ├── GroupFunction.cs    # REST API: Create/Join groups
│   │   └── WebSocketFunction.cs # WebSocket: Pomodoro sync, Kanban, Chat relay
│   └── StudyRoom.Lambda.csproj
└── StudyRoom.Lambda.Tests/
```

## Setup
> To be configured in a future milestone.
