using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Domain;
using Yodaphone.Web.Services;
using ChatPage = Yodaphone.Web.Components.Pages.Chat;
using MessageBubble = Yodaphone.Web.Components.Chat.ChatMessage;
using ChatWindow = Yodaphone.Web.Components.Chat.ChatWindow;
using Sidebar = Yodaphone.Web.Components.Chat.Sidebar;

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
        Assert.Contains("New conversation", html);
        Assert.Contains("yp-sidebar__inquiry--active", html);
    }

    [Fact]
    public async Task Sidebar_PreviewsFirstCustomerMessageAndMarksSelectedConversation()
    {
        var first = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        first.AddMessage(MessageRole.System, "Private system instructions");
        first.AddMessage(MessageRole.Assistant, "Welcome greeting");
        var firstText = new string('A', 39) + "👩‍💻" + " rest of a long first message";
        first.AddMessage(MessageRole.Customer, firstText);
        first.AddMessage(MessageRole.Customer, "Later customer message");
        first.Close();
        var second = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        second.AddMessage(MessageRole.Assistant, "Another welcome greeting");

        var html = await RenderAsync<Sidebar>(new()
        {
            ["Conversations"] = new[] { first, second },
            ["SelectedConversationId"] = first.Id
        });
        var buttons = Regex.Matches(html, "<button[^>]*class=\"yp-sidebar__inquiry[^\"]*\"[^>]*>(.*?)</button>", RegexOptions.Singleline);

        Assert.Equal(2, buttons.Count);
        Assert.Equal(new string('A', 39) + "👩‍💻…", WebUtility.HtmlDecode(buttons[0].Groups[1].Value).Trim());
        Assert.Equal("New conversation", buttons[1].Groups[1].Value.Trim());
        Assert.Contains("aria-current=\"true\"", buttons[0].Value);
        Assert.DoesNotContain("aria-current", buttons[1].Value);
        Assert.Contains(firstText, WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("Private system instructions", html);
        Assert.DoesNotContain("Welcome greeting", html);
        Assert.DoesNotContain("Later customer message", html);
    }

    [Fact]
    public async Task ChatPage_LoadsSavedConversationsWithoutCreatingAnotherChat()
    {
        var repository = new TestConversationRepository();
        var closed = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        closed.AddMessage(MessageRole.Customer, "Earlier saved question");
        closed.Close();
        var active = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        active.AddMessage(MessageRole.Customer, "Current saved question");
        active.AddMessage(MessageRole.Assistant, "Saved reply");
        await repository.AddAsync(closed, 1);
        await repository.AddAsync(active, 1);

        var html = await RenderAsync<ChatPage>(new(), repository);

        Assert.Contains("Earlier saved question", html);
        Assert.Contains("Current saved question", html);
        Assert.Contains("Saved reply", html);
        Assert.DoesNotContain("How can I help with your account today?", html);
        Assert.Equal(2, (await repository.GetAllAsync(1)).Count);
    }

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters,
        IConversationRepository? repository = null) where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();
        services.AddScoped<IChatAgent, OfflineChatAgent>();
        services.AddScoped<IConversationRepository>(_ => repository ?? new TestConversationRepository());
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

        public Task<IReadOnlyList<Conversation>> GetAllAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Conversation>>(conversations.Values
                .OrderByDescending(conversation => conversation.LastActivityAt).ToArray());

        public Task<Conversation?> GetLatestActiveAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(conversations.Values.LastOrDefault(conversation => conversation.Status == ConversationStatus.Active));

        public Task SaveAsync(Conversation conversation, int userId, CancellationToken cancellationToken = default)
        {
            conversations[conversation.Id] = conversation;
            return Task.CompletedTask;
        }
    }
}
