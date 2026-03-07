using Atlas.LicenseIssuer.Services;

namespace Atlas.LicenseIssuer.Forms;

public sealed partial class KeyManagementForm : Form
{
    private readonly KeyManagementService _keyMgmt;

    public KeyManagementForm(KeyManagementService keyMgmt)
    {
        _keyMgmt = keyMgmt;
        InitializeComponent();
        LoadPublicKey();
    }

    private void LoadPublicKey()
    {
        _pubkeyBox.Text = _keyMgmt.IsKeyInitialized()
            ? _keyMgmt.ExportPublicKeyPem()
            : "（尚未初始化密钥对）";
    }

    private void CopyBtn_Click(object? sender, EventArgs e)
    {
        Clipboard.SetText(_pubkeyBox.Text);
        MessageBox.Show("公钥已复制到剪贴板", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RegenBtn_Click(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            "重新生成密钥对后，旧公钥将失效，已颁发的证书无法被新平台验证。\n确定继续吗？",
            "警告",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        using var dlg = new InitKeyForm(_keyMgmt);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            LoadPublicKey();
            MessageBox.Show("密钥对已重新生成", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
