namespace SmmBot.Infrastructure.Clients.Dto;

public class ChatCompletionResponse
{
    public Choice[] Choices { get; set; }
}

public class Choice
{
    public Message Message { get; set; }
}

public class Message
{
    public string Content { get; set; }
    public string Role { get; set; }
}