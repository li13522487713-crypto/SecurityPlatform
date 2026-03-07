using Atlas.LicenseIssuer.Services;

namespace Atlas.LicenseIssuer.Forms;

public sealed partial class LoginForm : Form
{
    private readonly KeyManagementService _keyMgmt;

    public LoginForm(KeyManagementService keyMgmt)
    {
        _keyMgmt = keyMgmt;
        InitializeComponent();
        _initBtn.Visible = !_keyMgmt.IsKeyInitialized();
    }

    private void TryLogin()
    {
        var password = ReadInputText(_passwordBox);
        if (string.IsNullOrWhiteSpace(password))
        {
            _statusLabel.Text = "请输入颁发密码";
            return;
        }

        if (!_keyMgmt.IsKeyInitialized())
        {
            _statusLabel.Text = "尚未初始化密钥对，请先点击「初始化密钥」";
            return;
        }

        if (_keyMgmt.TryLoadPrivateKey(password))
        {
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _statusLabel.Text = "密码错误，请重试";
            ClearInputText(_passwordBox);
            _passwordBox.Focus();
        }
    }

    private void OnInitKey(object? sender, EventArgs e)
    {
        using var dlg = new InitKeyForm(_keyMgmt);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _initBtn.Visible = false;
            _statusLabel.Text = "密钥初始化成功，请使用新设置的密码登录";
            _statusLabel.ForeColor = Color.Green;
        }
    }

    private void PasswordBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            TryLogin();
        }
    }

    private void LoginBtn_Click(object? sender, EventArgs e) => TryLogin();

    private static string ReadInputText(AntdUI.Input input)
    {
        var text = input.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        // AntdUI 某些版本将值暴露为 Value，这里做兼容读取，避免 UI 有输入但取值为空。
        var valueProperty = input.GetType().GetProperty("Value");
        if (valueProperty?.GetValue(input) is string value && !string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return string.Empty;
    }

    private static void ClearInputText(AntdUI.Input input)
    {
        input.Text = string.Empty;
        var valueProperty = input.GetType().GetProperty("Value");
        if (valueProperty?.CanWrite is true)
        {
            valueProperty.SetValue(input, string.Empty);
        }
    }
}
