namespace BeniceSoft.Abp.EventBus.Dtm;

public interface IActionApiTokenChecker
{
    Task<bool> IsCorrectAsync(string token);
}
