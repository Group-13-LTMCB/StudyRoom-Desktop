using Amazon.CDK;

namespace StudyRoomInfra;

public static class Program
{
    public static void Main(string[] args)
    {
        var app = new App();
        _ = new StudyRoomStack(app, "StudyRoomStack", new StackProps
        {
            Description = "AWS Serverless Infrastructure for StudyRoom Desktop App"
        });
        app.Synth();
    }
}
