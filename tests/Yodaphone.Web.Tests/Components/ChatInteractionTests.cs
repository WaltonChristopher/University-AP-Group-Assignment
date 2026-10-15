using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Chat.Agents.Copilot;
using Yodaphone.Web.Domain;
using Yodaphone.Web.Services;
using Yodaphone.Web.Tests.Chat.Agents.TestDoubles;
using Yodaphone.Web.Tests.Services;
using ChatInput = Yodaphone.Web.Components.Chat.ChatInput;
using ChatWindow = Yodaphone.Web.Components.Chat.ChatWindow;
using Sidebar = Yodaphone.Web.Components.Chat.Sidebar;
using ChatPage = Yodaphone.Web.Components.Pages.Chat;

namespace Yodaphone.Web.Tests.Components;

public sealed class ChatInteractionTests : BunitContext
{
    public ChatInteractionTests() => JSInterop.SetupVoid("chatInterop.scrollToBottom", _ => true).SetVoidResult();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Input_SendsDraftAndClearsIt(bool useEnter)
    {
        var sent = new List<string>();
        var cut = Render<ChatInput>(p => p.Add(c => c.OnMessageSent, (string text) => sent.Add(text)));
        cut.Find("textarea").Input("My question");
        if (useEnter) cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        else cut.Find("button").Click();
        Assert.Equal(new[] { "My question" }, sent);
        Assert.Equal("", cut.Find("textarea").GetAttribute("value"));
        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData("Enter", true)]
    [InlineData("a", false)]
    public void Input_ShiftEnterAndOtherKeysDoNotSend(string key, bool shift)
    {
        var sent = new List<string>();
        var cut = Render<ChatInput>(p => p.Add(c => c.OnMessageSent, (string text) => sent.Add(text)));
        cut.Find("textarea").Input("Keep this draft");
        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = key, ShiftKey = shift });
        Assert.Empty(sent);
        Assert.Equal("Keep this draft", cut.Find("textarea").GetAttribute("value"));
    }

    [Theory]
    [InlineData(true, false, "Question")]
    [InlineData(false, true, "Question")]
    [InlineData(false, false, " \t")]
    public void Input_BlockedSendPreservesDraft(bool sending, bool disabled, string draft)
    {
        var sent = new List<string>();
        var cut = Render<ChatInput>(p => p.Add(c => c.IsSending, sending).Add(c => c.IsDisabled, disabled)
            .Add(c => c.OnMessageSent, (string text) => sent.Add(text)));
        cut.Find("textarea").Input(draft);
        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Empty(sent);
        Assert.Equal(draft, cut.Find("textarea").GetAttribute("value"));
        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void Input_NullChangeResetsDraft()
    {
        var cut = Render<ChatInput>();
        cut.Find("textarea").Input("Draft");
        cut.Find("textarea").TriggerEvent("oninput", new ChangeEventArgs { Value = null });
        Assert.Equal("", cut.Find("textarea").GetAttribute("value"));
        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void Window_ScrollsOnFirstRenderNewMessagesAndTypingOnly()
    {
        var cut = Render<ChatWindow>();
        Assert.Contains("Send a message to start", cut.Markup);
        Assert.Single(JSInterop.Invocations);
        cut.Render(p => p.Add(c => c.IsBusy, true));
        Assert.Single(JSInterop.Invocations);
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        conversation.AddMessage(MessageRole.Customer, "Hello");
        cut.Render(p => p.Add(c => c.Messages, conversation.Messages));
        Assert.Equal(2, JSInterop.Invocations.Count);
        cut.Render(p => p.Add(c => c.IsBotTyping, true));
        Assert.Equal(3, JSInterop.Invocations.Count);
        Assert.Single(cut.FindAll(".yp-typing"));
        Assert.All(JSInterop.Invocations, invocation => Assert.Equal("chatInterop.scrollToBottom", invocation.Identifier));
    }

    [Fact]
    public void Window_NewConversationInvokesCallback()
    {
        var calls = 0;
        var cut = Render<ChatWindow>(p => p.Add(c => c.OnNewConversation, () => calls++));
        cut.Find("button").Click();
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Sidebar_EmptyStateHelpAndThemeCallbacks()
    {
        var help = 0;
        var toggles = 0;
        var cut = Render<Sidebar>(p => p.Add(c => c.OnHelpRequested, () => help++)
            .Add(c => c.OnDarkModeToggled, () => toggles++));
        Assert.Contains("No conversations yet", cut.Markup);
        cut.Find(".yp-sidebar__item").Click();
        cut.Find(".yp-sidebar__item--toggle").Click();
        Assert.Equal(1, help);
        Assert.Equal(1, toggles);
        cut.Render(p => p.Add(c => c.DarkMode, true).Add(c => c.CanShowHelp, false));
        Assert.Contains("Light mode", cut.Markup);
        Assert.True(cut.Find(".yp-sidebar__item").HasAttribute("disabled"));
    }

    [Fact]
    public void Sidebar_SelectsConversationById()
    {
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        conversation.AddMessage(MessageRole.Customer, "Short question");
        Guid? selected = null;
        var cut = Render<Sidebar>(p => p.Add(c => c.Conversations, new[] { conversation })
            .Add(c => c.OnConversationSelected, (Guid id) => selected = id));
        cut.Find(".yp-sidebar__inquiry").Click();
        Assert.Equal(conversation.Id, selected);
        Assert.Equal("Short question", cut.Find(".yp-sidebar__inquiry").TextContent.Trim());
    }

    [Fact]
    public async Task Page_SendsTrimmedMessageAndReplacesPendingBubbleWithSavedReply()
    {
        var service = new TestChatService();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.OnSend = async (conversation, text, token) =>
        {
            await gate.Task.WaitAsync(token);
            return conversation.AddMessage(MessageRole.Assistant, "Saved answer");
        };
        var cut = RenderPage(service);
        cut.Find("textarea").Input("  My question  ");
        var send = cut.Find(".yp-input-bar__send").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Contains("Sending", cut.Markup));
        Assert.Single(cut.FindAll(".yp-typing"));
        Assert.All(cut.FindAll(".yp-chat-window__toolbar button, textarea"), element => Assert.True(element.HasAttribute("disabled")));
        gate.SetResult();
        await send;
        cut.WaitForAssertion(() => Assert.Contains("Saved answer", cut.Markup));
        Assert.Equal("My question", service.SentTexts.Single());
        Assert.Single(cut.FindAll(".yp-message-row--user"));
        Assert.DoesNotContain("Sending", cut.Markup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Page_FailedReplyKeepsCustomerMessageAndShowsSafeError(bool agentFailure)
    {
        var service = new TestChatService();
        Exception failure = agentFailure ? new ChatAgentException(new Exception("private credential")) : new Exception("private database error");
        service.OnSend = (_, _, _) => throw failure;
        var cut = RenderPage(service);
        cut.Find("textarea").Input("Retain my question");
        cut.Find(".yp-input-bar__send").Click();
        var expected = agentFailure ? failure.Message : "Your message could not be completed. Please try again.";
        Assert.Equal(expected, cut.Find("[role=alert]").TextContent);
        Assert.Single(cut.FindAll(".yp-message-row--user"));
        Assert.DoesNotContain("private", cut.Markup);
        Assert.DoesNotContain("Sending", cut.Markup);
    }

    [Fact]
    public void Page_HelpThemeNewChatAndSelectionPreserveHistory()
    {
        var service = new TestChatService();
        var cut = RenderPage(service);
        var first = service.Conversations.Single();
        cut.Find(".yp-sidebar__item").Click();
        Assert.Contains("Need help?", cut.Markup);
        cut.Find(".yp-sidebar__item--toggle").Click();
        Assert.Contains("dark", cut.Find(".yp-shell").ClassList);
        cut.Find(".yp-chat-window__toolbar button").Click();
        Assert.Equal(2, service.Conversations.Count);
        cut.FindAll(".yp-sidebar__inquiry")[0].Click();
        Assert.Contains("Need help?", cut.Markup);
        var loads = service.LoadCount;
        cut.FindAll(".yp-sidebar__inquiry")[0].Click();
        Assert.Equal(loads, service.LoadCount);
        Assert.Contains(first.Id.ToString(), service.LoadedIds);
    }

    [Fact]
    public void Page_SelectingClosedConversationDisablesInputAndHelp()
    {
        var service = new TestChatService();
        var closed = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        closed.AddMessage(MessageRole.Customer, "Closed question");
        closed.Close();
        service.Conversations.Add(closed);
        var cut = RenderPage(service);
        cut.FindAll(".yp-sidebar__inquiry")[0].Click();
        Assert.Contains("This conversation is closed", cut.Markup);
        Assert.True(cut.Find("textarea").HasAttribute("disabled"));
        Assert.True(cut.Find(".yp-sidebar__item").HasAttribute("disabled"));
    }

    [Fact]
    public void Page_RestoreFailureStartsFreshConversation()
    {
        var service = new TestChatService { FailActiveLoad = true };
        var cut = RenderPage(service);
        Assert.Contains("How can I help with your account today?", cut.Markup);
        Assert.Single(service.Conversations);
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData("start", "A new chat could not be started.")]
    [InlineData("load", "The conversation could not be loaded.")]
    [InlineData("help", "Help could not be displayed.")]
    [InlineData("refresh", "The conversation could not be refreshed.")]
    [InlineData("list", "Your conversations could not be refreshed.")]
    public void Page_OperationFailuresShowErrorAndRestoreControls(string operation, string expected)
    {
        var service = new TestChatService();
        var cut = RenderPage(service);
        service.Failure = operation;
        if (operation == "start") cut.Find(".yp-chat-window__toolbar button").Click();
        else if (operation == "load")
        {
            service.Conversations.Add(new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow));
            // Refresh the list through help before selecting the added chat.
            cut.Find(".yp-sidebar__item").Click();
            cut.FindAll(".yp-sidebar__inquiry")[1].Click();
        }
        else cut.Find(".yp-sidebar__item").Click();
        Assert.Contains(expected, cut.Find("[role=alert]").TextContent);
        Assert.False(cut.Find(".yp-chat-window__toolbar button").HasAttribute("disabled"));
        Assert.DoesNotContain("private", cut.Markup);
    }

    [Fact]
    public async Task Page_DisposalCancelsPendingReply()
    {
        var service = new TestChatService();
        CancellationToken received = default;
        service.OnSend = async (_, _, token) =>
        {
            received = token;
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Unreachable");
        };
        var cut = RenderPage(service);
        cut.Find("textarea").Input("Question");
        var send = cut.Find(".yp-input-bar__send").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.True(received.CanBeCanceled));
        // Cancel the page lifetime before the renderer waits for pending events.
        await cut.InvokeAsync(() => cut.Instance.Dispose());
        await send;
        Assert.True(received.IsCancellationRequested);
        Assert.Single(service.SentTexts);
    }

    [Fact]
    public async Task Page_IgnoresBlankMessagesAndClosedConversationCallbacks()
    {
        var service = new TestChatService();
        var cut = RenderPage(service);
        await cut.InvokeAsync(() => cut.FindComponent<ChatInput>().Instance.OnMessageSent.InvokeAsync(" \t"));
        Assert.Empty(service.SentTexts);
        var conversation = service.Conversations.Single();
        conversation.Close();
        var before = conversation.Messages.Count;
        await cut.InvokeAsync(() => cut.FindComponent<ChatInput>().Instance.OnMessageSent.InvokeAsync("Question"));
        await cut.InvokeAsync(() => cut.FindComponent<Sidebar>().Instance.OnHelpRequested.InvokeAsync());
        Assert.Empty(service.SentTexts);
        Assert.Equal(before, conversation.Messages.Count);
    }

    [Fact]
    public async Task Page_IgnoresQueuedOperationsWhileSendIsBusy()
    {
        var service = new TestChatService();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.OnSend = async (conversation, _, token) =>
        {
            await gate.Task.WaitAsync(token);
            return conversation.AddMessage(MessageRole.Assistant, "Answer");
        };
        var cut = RenderPage(service);
        cut.Find("textarea").Input("Question");
        var send = cut.Find(".yp-input-bar__send").ClickAsync(new MouseEventArgs());
        try
        {
            cut.WaitForAssertion(() => Assert.Contains("Sending", cut.Markup));
            await cut.InvokeAsync(() => cut.FindComponent<ChatWindow>().Instance.OnNewConversation.InvokeAsync());
            await cut.InvokeAsync(() => cut.FindComponent<Sidebar>().Instance.OnHelpRequested.InvokeAsync());
            Assert.Single(service.Conversations);
            Assert.DoesNotContain("Need help?", cut.Markup);
        }
        finally { gate.TrySetResult(); }
        await send;
        Assert.Contains("Answer", cut.Markup);
        Assert.Single(service.SentTexts);
    }

    [Fact]
    public async Task Page_RecoveryAutomaticallyRestoresOnlineRepliesAndPersistsOneVisibleNotice()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var factory = new FakeCopilotClientFactory { Response = null };
        var options = Options.Create(new CopilotOptions { ResponseTimeoutSeconds = 1 });
        var check = new CopilotConnectionHealthCheck(factory, options,
            NullLogger<CopilotConnectionHealthCheck>.Instance, TimeProvider.System);
        var agent = new CopilotFallbackChatAgent(new GitHubCopilotAgent(factory), new OfflineChatAgent(), options,
            NullLogger<CopilotFallbackChatAgent>.Instance, check);
        var service = new ChatService(agent, new SqliteConversationRepository(database), new TestCurrentUser());
        Services.AddSingleton<IChatService>(service);
        var cut = Render<ChatPage>();
        cut.WaitForAssertion(() => Assert.Contains("How can I help with your account today?", cut.Markup));
        cut.Find("textarea").Input("Help with my bill");
        await cut.Find(".yp-input-bar__send").ClickAsync(new MouseEventArgs());
        Assert.Contains("chat service is currently unavailable", cut.Markup);
        factory.Response = FakeCopilotClientFactory.CreateResponse("Online answer");

        cut.WaitForAssertion(() => Assert.Contains("Connection to the Yodaphone chatbot has been restored.", cut.Markup),
            TimeSpan.FromSeconds(10));

        cut.Find("textarea").Input("Can you help now?");
        await cut.Find(".yp-input-bar__send").ClickAsync(new MouseEventArgs());
        Assert.Contains("Online answer", cut.Markup);
        var restored = await service.GetActiveConversationAsync();
        Assert.NotNull(restored);
        Assert.Equal(6, restored.Messages.Count);
        Assert.Equal(new[] { "Help with my bill", "Can you help now?" },
            restored.Messages.Where(message => message.Role == MessageRole.Customer).Select(message => message.Content));
        var notice = Assert.Single(restored.Messages, message => message.Content.StartsWith("Connection to the Yodaphone chatbot"));
        Assert.Equal(MessageRole.Assistant, notice.Role);
        Assert.DoesNotContain("test-model", cut.Markup);
        Assert.Single(factory.Calls, call => call == "list-models");
        Assert.Equal(2, factory.Calls.Count(call => call == "send"));
        await cut.InvokeAsync(() => cut.Instance.Dispose());
        database.AssertAllContextsDisposed();
    }

    [Fact]
    public async Task Page_RecoveryNotifiesTheConversationSelectedWhenTheCheckCompletes()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subsequentCheck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new TestChatService { ConnectionRetryInterval = TimeSpan.FromMilliseconds(20) };
        var attempts = 0;
        service.OnRestoreConnection = async token =>
        {
            if (++attempts == 1)
            {
                started.TrySetResult();
                return await gate.Task.WaitAsync(token);
            }
            subsequentCheck.TrySetResult();
            return false;
        };
        var cut = RenderPage(service);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cut.Find(".yp-chat-window__toolbar button").Click();
        gate.SetResult(true);

        cut.WaitForAssertion(() => Assert.Contains("Connection to the Yodaphone chatbot has been restored.", cut.Markup));
        await subsequentCheck.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.DoesNotContain(service.Conversations[0].Messages,
            message => message.Content.StartsWith("Connection to the Yodaphone chatbot"));
        Assert.Single(service.Conversations[1].Messages,
            message => message.Content.StartsWith("Connection to the Yodaphone chatbot"));
    }

    [Fact]
    public async Task Page_DisposalCancelsAnInFlightRecoveryCheck()
    {
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new TestChatService { ConnectionRetryInterval = TimeSpan.FromMilliseconds(20) };
        service.OnRestoreConnection = async token =>
        {
            started.TrySetResult(token);
            try
            {
                await Task.Delay(Timeout.Infinite, token);
                return true;
            }
            finally
            {
                cancelled.TrySetResult();
            }
        };
        var cut = RenderPage(service);
        var received = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await cut.InvokeAsync(() => cut.Instance.Dispose());
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(received.IsCancellationRequested);
        Assert.DoesNotContain(service.Conversations.Single().Messages,
            message => message.Content.StartsWith("Connection to the Yodaphone chatbot"));
    }

    private IRenderedComponent<ChatPage> RenderPage(TestChatService service)
    {
        Services.AddSingleton<IChatService>(service);
        return Render<ChatPage>();
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public int UserId => 42;
    }

    private sealed class TestChatService : IChatService
    {
        public TimeSpan? ConnectionRetryInterval { get; init; }
        public Func<CancellationToken, Task<bool>>? OnRestoreConnection { get; set; }
        public Task<bool> TryRestoreConnectionAsync(CancellationToken cancellationToken = default) =>
            OnRestoreConnection?.Invoke(cancellationToken) ?? Task.FromResult(false);

        public List<Conversation> Conversations { get; } = [];
        public List<string> SentTexts { get; } = [];
        public List<string> LoadedIds { get; } = [];
        public int LoadCount => LoadedIds.Count;
        public bool FailActiveLoad { get; init; }
        public string? Failure { get; set; }
        public Func<Conversation, string, CancellationToken, Task<Yodaphone.Web.Domain.ChatMessage>>? OnSend { get; set; }

        public Task<Conversation> StartConversationAsync(CancellationToken cancellationToken = default)
        {
            Fail("start");
            var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
            Conversations.Add(conversation);
            return Task.FromResult(conversation);
        }
        public Task<Conversation> GetConversationAsync(string id, CancellationToken cancellationToken = default)
        {
            LoadedIds.Add(id);
            Fail("load"); Fail("refresh");
            return Task.FromResult(Conversations.Single(c => c.Id.ToString() == id));
        }
        public Task<IReadOnlyList<Conversation>> GetConversationsAsync(CancellationToken cancellationToken = default)
        {
            Fail("list");
            return Task.FromResult<IReadOnlyList<Conversation>>(Conversations.ToArray());
        }
        public Task<Conversation?> GetActiveConversationAsync(CancellationToken cancellationToken = default) =>
            FailActiveLoad ? throw new Exception("Restore failed") : Task.FromResult(Conversations.LastOrDefault(c => c.Status == ConversationStatus.Active));
        public Task<Yodaphone.Web.Domain.ChatMessage> AddMessageAsync(string id, MessageRole role, string content, CancellationToken cancellationToken = default)
        {
            Fail("help");
            return Task.FromResult(Conversations.Single(c => c.Id.ToString() == id).AddMessage(role, content));
        }
        public Task<Yodaphone.Web.Domain.ChatMessage> SendMessageAsync(string id, string content, CancellationToken cancellationToken = default)
        {
            SentTexts.Add(content);
            var conversation = Conversations.Single(c => c.Id.ToString() == id);
            conversation.AddMessage(MessageRole.Customer, content);
            return OnSend?.Invoke(conversation, content, cancellationToken)
                ?? Task.FromResult(conversation.AddMessage(MessageRole.Assistant, "Reply"));
        }
        public Task CloseConversationAsync(string id, CancellationToken cancellationToken = default)
        {
            Conversations.Single(c => c.Id.ToString() == id).Close();
            return Task.CompletedTask;
        }
        private void Fail(string operation)
        {
            if (Failure == operation) throw new Exception("private service failure");
        }
    }
}
