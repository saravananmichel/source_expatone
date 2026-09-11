namespace ExpatOne.Application.Interfaces;

public interface INotificationService
{
    Task SendPushNotificationAsync(string userId, string title, string body, Dictionary<string, string>? data = null);
    Task SendBulkNotificationAsync(IEnumerable<string> userIds, string title, string body);
}
