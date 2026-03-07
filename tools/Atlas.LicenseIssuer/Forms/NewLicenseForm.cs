using Atlas.LicenseIssuer.Models;
using Atlas.LicenseIssuer.Services;

namespace Atlas.LicenseIssuer.Forms;

public sealed partial class NewLicenseForm : Form
{
    private readonly LicenseSigningService _signingService;
    private readonly IssuanceLogService _logService;
    private readonly CustomerRecord _customer;
    private readonly LicensePayload? _renewFrom;

    public NewLicenseForm(
        LicenseSigningService signingService,
        IssuanceLogService logService,
        CustomerRecord customer,
        LicensePayload? renewFrom = null)
    {
        _signingService = signingService;
        _logService = logService;
        _customer = customer;
        _renewFrom = renewFrom;
        InitializeComponent();
        _editionCombo.SelectedIndexChanged += OnEditionChanged;
        _permanentRadio.CheckedChanged += PermanentRadio_CheckedChanged;
        _bindRadio.CheckedChanged += BindRadio_CheckedChanged;
        _signBtn.Click += OnSign;
        _cancelBtn.Click += CancelBtn_Click;
        ApplyDefaults();
    }

    private void ApplyDefaults()
    {
        Text = _renewFrom is null ? $"新建证书 — {_customer.Name}" : $"续签证书 — {_customer.Name}";
        if (_renewFrom is null) return;

        _editionCombo.SelectedItem = _renewFrom.Edition;
        _permanentRadio.Checked = _renewFrom.IsPermanent;
        if (_renewFrom.ExpiresAt.HasValue)
            _expiryPicker.Value = _renewFrom.ExpiresAt.Value.LocalDateTime;

        var features = _renewFrom.Features;
        _chkLowCode.Checked = features.TryGetValue("lowCode", out var v) && v;
        _chkWorkflow.Checked = features.TryGetValue("workflow", out v) && v;
        _chkApproval.Checked = features.TryGetValue("approval", out v) && v;
        _chkAlert.Checked = features.TryGetValue("alert", out v) && v;
        _chkOffline.Checked = features.TryGetValue("offlineDeploy", out v) && v;
        _chkMultiTenant.Checked = features.TryGetValue("multiTenant", out v) && v;

        var limits = _renewFrom.Limits;
        if (limits.TryGetValue("maxApps", out var la)) _maxApps.Value = la;
        if (limits.TryGetValue("maxUsers", out var lu)) _maxUsers.Value = lu;
        if (limits.TryGetValue("maxTenants", out var lt)) _maxTenants.Value = lt;

        if (!string.IsNullOrWhiteSpace(_renewFrom.MachineFingerprint))
        {
            _bindRadio.Checked = true;
            _fingerprintBox.Text = _renewFrom.MachineFingerprint;
        }
    }

    private void OnEditionChanged(object? sender, EventArgs e)
    {
        var edition = _editionCombo.SelectedItem?.ToString() ?? "Trial";
        var features = LicenseFeatures.ForEdition(edition);
        _chkLowCode.Checked = features.LowCode;
        _chkWorkflow.Checked = features.Workflow;
        _chkApproval.Checked = features.Approval;
        _chkAlert.Checked = features.Alert;
        _chkOffline.Checked = features.OfflineDeploy;
        _chkMultiTenant.Checked = features.MultiTenant;

        var limits = LicenseLimits.ForEdition(edition);
        _maxApps.Value = limits.MaxApps < 0 ? 0 : limits.MaxApps;
        _maxUsers.Value = limits.MaxUsers < 0 ? 0 : limits.MaxUsers;
        _maxTenants.Value = limits.MaxTenants < 0 ? 0 : limits.MaxTenants;
    }

    private void OnSign(object? sender, EventArgs e)
    {
        var edition = _editionCombo.SelectedItem?.ToString() ?? "Trial";
        var isPermanent = _permanentRadio.Checked;
        DateTimeOffset? expiresAt = null;
        if (!isPermanent)
        {
            // DateTimePicker 返回本地日期；先构造本地“当日 23:59:59.9999999”，再转 UTC 统一落库/签发
            var localEndOfDay = DateTime.SpecifyKind(
                _expiryPicker.Value.Date.AddDays(1).AddTicks(-1),
                DateTimeKind.Local);
            expiresAt = new DateTimeOffset(localEndOfDay).ToUniversalTime();
        }

        var payload = new LicensePayload
        {
            LicenseId = _renewFrom?.LicenseId ?? Guid.NewGuid(),
            Revision = (_renewFrom?.Revision ?? 0) + 1,
            CustomerId = _customer.Id,
            TenantName = _customer.Name,
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            IsPermanent = isPermanent,
            Edition = edition,
            MachineFingerprint = _bindRadio.Checked ? _fingerprintBox.Text.Trim() : null,
            Features = new Dictionary<string, bool>
            {
                ["lowCode"] = _chkLowCode.Checked,
                ["workflow"] = _chkWorkflow.Checked,
                ["approval"] = _chkApproval.Checked,
                ["alert"] = _chkAlert.Checked,
                ["offlineDeploy"] = _chkOffline.Checked,
                ["multiTenant"] = _chkMultiTenant.Checked,
                ["audit"] = true
            },
            Limits = new Dictionary<string, int>
            {
                ["maxApps"] = (int)_maxApps.Value == 0 ? -1 : (int)_maxApps.Value,
                ["maxUsers"] = (int)_maxUsers.Value == 0 ? -1 : (int)_maxUsers.Value,
                ["maxTenants"] = (int)_maxTenants.Value == 0 ? -1 : (int)_maxTenants.Value,
                ["auditRetentionDays"] = 180,
            }
        };

        try
        {
            var exportedContent = _signingService.SignAndExport(payload);

            // 验证签名
            if (!_signingService.VerifyExported(exportedContent))
            {
                MessageBox.Show("签名自检失败，请联系技术支持", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 保存文件
            var defaultName = $"{_customer.Name}_{edition}_{DateTime.Today:yyyyMMdd}.atlaslicense";
            using var dlg = new SaveFileDialog
            {
                Filter = "Atlas License (*.atlaslicense)|*.atlaslicense",
                FileName = defaultName
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            File.WriteAllText(dlg.FileName, exportedContent);

            // 写颁发日志
            _logService.Append(new IssuanceLogEntry
            {
                CustomerId = _customer.Id,
                LicenseId = payload.LicenseId.ToString(),
                Revision = payload.Revision,
                Edition = edition,
                Action = _renewFrom is null ? "NEW" : "RENEW",
                IssuedAt = DateTimeOffset.UtcNow.ToString("o"),
                ExpiresAt = expiresAt?.ToString("o"),
                IsPermanent = isPermanent,
                Remark = _remarkBox.Text.Trim()
            });

            MessageBox.Show($"证书已成功导出至：\n{dlg.FileName}", "成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"颁发失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PermanentRadio_CheckedChanged(object? sender, EventArgs e)
    {
        _expiryPicker.Enabled = !_permanentRadio.Checked;
    }

    private void BindRadio_CheckedChanged(object? sender, EventArgs e) => _fingerprintBox.Enabled = _bindRadio.Checked;

    private void CancelBtn_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
