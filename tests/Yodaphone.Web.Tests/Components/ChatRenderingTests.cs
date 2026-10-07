using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Xunit;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Domain;
using Yodaphone.Web.Services;
using ChatPage = Yodaphone.Web.Components.Pages.Chat;
using MessageBubble = Yodaphone.Web.Components.Chat.ChatMessage;
using ChatWindow = Yodaphone.Web.Components.Chat.ChatWindow;

namespace Yodaphone.Web.Tests.Components;

public sealed class ChatRenderingTests
{
    [Theory]
    [InlineData(MessageRole.Customer, "yp-message-row--user", "You")]
    [InlineData(MessageRole.Assistant, "yp-message-row--bot", "YodaPhone AI")]
    public async Task MessageBubble_UsesDomainRoleAndContent(MessageRole role, string cssClass, string sender)
    {
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var message = conversation.AddMessage(role, "Test message");

        var html = await RenderAsync<MessageBubble>(new() { ["Message"] = message });

        Assert.Contains(cssClass, html);
        Assert.Contains(sender, html);
        Assert.Contains("Test message", html);
        Assert.DoesNotContain("Sending", html);
    }

    [Fact]
    public async Task ChatWindow_HidesSystemInstructionsAndHasNoPendingBubbleByDefault()
    {
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        conversation.AddMessage(MessageRole.System, "Private system instructions");
        conversation.AddMessage(MessageRole.Assistant, "Visible reply");

        var html = await RenderAsync<ChatWindow>(new() { ["Messages"] = conversation.Messages });

        Assert.Contains("Visible reply", html);
        Assert.DoesNotContain("Private system instructions", html);
        Assert.DoesNotContain("Sending", html);
        Assert.DoesNotContain("PendingMessage", html);
    }

    [Fact]
    public async Task ChatWindow_RendersPendingTextWithoutCreatingADomainMessage()
    {
        var html = await RenderAsync<ChatWindow>(new() { ["PendingMessage"] = "Pending customer text" });

        Assert.Contains("Pending customer text", html);
        Assert.Contains("Sending", html);
        Assert.Contains("yp-message-row--user", html);
    }

    [Fact]
    public async Task ChatPage_InitializesThroughServiceAndRendersWelcomeWithoutPendingBubble()
    {
        var html = await RenderAsync<ChatPage>(new());

        Assert.Contains("How can I help with your account today?", html);
        Assert.DoesNotContain("Sending", html);
        Assert.DoesNotContain("_pendingMessage", html);
        Assert.DoesNotContain("yp-chat-error", html);
    }

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();
        services.AddScoped<IChatAgent, OfflineChatAgent>();
        services.AddScoped<IConversationRepository, TestConversationRepository>();
        services.AddSingleton<ICurrentUser>(new TestCurrentUser());
        services.AddScoped<IChatService, ChatService>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(
            scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));
            return component.ToHtmlString();
        });
    }

    private sealed class NoOpJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public int UserId => 1;
    }

    private sealed class TestConversationRepository : IConversationRepository
    {
        private readonly Dictionary<Guid, Conversation> conversations = [];

        public Task AddAsync(Conversation conversation, int userId, CancellationToken cancellationToken = default)
        {
            conversations.Add(conversation.Id, conversation);
            return Task.CompletedTask;
        }

        public Task<Conversation?> GetAsync(Guid conversationId, int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(conversations.GetValueOrDefault(conversationId));

        public Task<Conversation?> GetLatestActiveAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(conversations.Values.LastOrDefault(conversation => conversation.Status == ConversationStatus.Active));

        public Task SaveAsync(Conversation conversation, int userId, CancellationToken cancellationToken = default)
        {
            conversations[conversation.Id] = conversation;
            return Task.CompletedTask;
        }
    }
}
