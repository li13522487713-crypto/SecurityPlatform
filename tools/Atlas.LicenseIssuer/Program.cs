using Atlas.LicenseIssuer.Data;
using Atlas.LicenseIssuer.Forms;
using Atlas.LicenseIssuer.Services;

namespace Atlas.LicenseIssuer;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 初始化本地数据库
        DbInitializer.Initialize();

        var keyMgmt = new KeyManagementService();
        var customerService = new CustomerService();
        var logService = new IssuanceLogService();
        var signingService = new LicenseSigningService(keyMgmt);

        // 启动时验证颁发密码（或引导初始化密钥对）
        using var loginForm = new LoginForm(keyMgmt);
        if (loginForm.ShowDialog() != DialogResult.OK)
            return;

        Application.Run(new MainForm(customerService, logService, signingService, keyMgmt));
    }
}
