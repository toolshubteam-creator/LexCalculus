namespace LexCalculus.Core.Messaging;

/// <summary>
/// Test ve fallback için no-op IMessagingNotifier. Üretimde
/// SignalRMessagingNotifier (Web katmanında) kullanılır; integration test'lerde
/// DI override ile bu impl register edilir (Hub açmadan testler hızlı koşar).
/// Faz 5.6.
/// </summary>
public sealed class NoOpMessagingNotifier : IMessagingNotifier
{
    public Task NotifyMessageReceivedAsync(int recipientId, int messageId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task NotifyMessageDeletedAsync(int recipientId, int conversationId, int messageId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task NotifyMessageHiddenAsync(
        int senderId, int recipientId, int conversationId, int messageId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task NotifyConversationReadAsync(int userId, int conversationId, CancellationToken ct = default)
        => Task.CompletedTask;
}
