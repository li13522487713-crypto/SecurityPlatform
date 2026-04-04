namespace Atlas.Presentation.Shared.Models;

public sealed record ChangePasswordViewModel(string CurrentPassword, string NewPassword, string ConfirmPassword);
