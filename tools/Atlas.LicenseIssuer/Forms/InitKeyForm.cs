using Atlas.LicenseIssuer.Services;

namespace Atlas.LicenseIssuer.Forms;

public sealed partial class InitKeyForm : Form
{
    private readonly KeyManagementService _keyMgmt;

    public InitKeyForm(KeyManagementService keyMgmt)
    {
        _keyMgmt = keyMgmt;
        InitializeComponent();
    }

    private void OkBtn_Click(object? sender, EventArgs e)
    {
        var password = ReadInputText(_passwordBox);
        var confirmPassword = ReadInputText(_confirmBox);

        if (password != confirmPassword)
        {
            _statusLabel.Text = "两次密码不一致";
            return;
        }

        if (password.Length < 8)
        {
            _statusLabel.Text = "密码长度不能少于8位";
            return;
        }

        try
        {
            _keyMgmt.GenerateKeyPair(password);
            _keyMgmt.TryLoadPrivateKey(password);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"生成失败：{ex.Message}";
        }
    }

    private void CancelBtn_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private static string ReadInputText(AntdUI.Input input)
    {
        var text = input.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var valueProperty = input.GetType().GetProperty("Value");
        if (valueProperty?.GetValue(input) is string value && !string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return string.Empty;
    }
}
