using System.Text;
using System.Text.Json;
using Atlas.LicenseIssuer.Models;
using Atlas.LicenseIssuer.Services;

namespace Atlas.LicenseIssuer.Forms;

public sealed partial class MainForm : Form
{
    private readonly CustomerService _customerService;
    private readonly IssuanceLogService _logService;
    private readonly LicenseSigningService _signingService;
    private readonly KeyManagementService _keyMgmt;

    private List<CustomerRecord> _allCustomers = [];
    private string? _selectedCustomerId;

    // 每位客户的颁发历史（本次会话内缓存）
    private readonly Dictionary<string, List<IssuanceLogEntry>> _logCache = new();

    public MainForm(
        CustomerService customerService,
        IssuanceLogService logService,
        LicenseSigningService signingService,
        KeyManagementService keyMgmt)
    {
        _customerService = customerService;
        _logService = logService;
        _signingService = signingService;
        _keyMgmt = keyMgmt;
        InitializeComponent();
        _searchBox.TextChanged += SearchBox_TextChanged;
        _customerList.SelectedIndexChanged += OnCustomerSelected;
        _newLicenseBtn.Click += OnNewLicense;
        _renewBtn.Click += OnRenew;
        _exportBtn.Click += OnExportLatest;
        LoadCustomers();
    }

    private void LoadCustomers()
    {
        _allCustomers = _customerService.GetAll();
        FilterCustomers();
    }

    private void FilterCustomers()
    {
        var keyword = _searchBox.Text.Trim().ToLowerInvariant();
        _customerList.DataSource = null;
        _customerList.DataSource = string.IsNullOrEmpty(keyword)
            ? _allCustomers
            : _allCustomers.Where(c => c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnCustomerSelected(object? sender, EventArgs e)
    {
        if (_customerList.SelectedItem is not CustomerRecord customer)
        {
            _selectedCustomerId = null;
            return;
        }

        _selectedCustomerId = customer.Id;
        _customerDetailLabel.Text = $"客户：{customer.Name}  联系方式：{customer.Contact ?? "—"}  创建：{customer.CreatedAt[..10]}";
        LoadCustomerLogs(customer.Id);
    }

    private void LoadCustomerLogs(string customerId)
    {
        if (!_logCache.TryGetValue(customerId, out var logs))
        {
            logs = _logService.GetByCustomer(customerId);
            _logCache[customerId] = logs;
        }

        _licenseGrid.DataSource = null;
        _licenseGrid.DataSource = logs;
    }

    private void OnAddCustomer(object? sender, EventArgs e)
    {
        using var dlg = new CustomerForm(_customerService);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _logCache.Clear();
            LoadCustomers();
        }
    }

    private void OnNewLicense(object? sender, EventArgs e)
    {
        if (_selectedCustomerId is null)
        {
            MessageBox.Show("请先选择客户", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var customer = _customerService.GetById(_selectedCustomerId);
        if (customer is null) return;

        using var dlg = new NewLicenseForm(_signingService, _logService, customer);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _logCache.Remove(_selectedCustomerId);
            LoadCustomerLogs(_selectedCustomerId);
        }
    }

    private void OnRenew(object? sender, EventArgs e)
    {
        if (_selectedCustomerId is null || _licenseGrid.SelectedRows.Count == 0)
        {
            MessageBox.Show("请先选择客户并在历史列表中选中要续签的证书记录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var customer = _customerService.GetById(_selectedCustomerId);
        if (customer is null) return;

        // 查找最新颁发记录
        var logs = _logService.GetByCustomer(_selectedCustomerId);
        if (logs.Count == 0) return;
        var latest = logs.OrderByDescending(x => x.Revision).First();

        // 构造续签 payload 默认值（仅提供 LicenseId 和 Revision）
        var renewFrom = new LicensePayload
        {
            LicenseId = Guid.TryParse(latest.LicenseId, out var gid) ? gid : Guid.NewGuid(),
            Revision = latest.Revision,
            Edition = latest.Edition,
            IsPermanent = latest.IsPermanent,
            ExpiresAt = string.IsNullOrEmpty(latest.ExpiresAt) ? null
                : DateTimeOffset.TryParse(latest.ExpiresAt, out var dt) ? dt : null
        };

        using var dlg = new NewLicenseForm(_signingService, _logService, customer, renewFrom);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _logCache.Remove(_selectedCustomerId);
            LoadCustomerLogs(_selectedCustomerId);
        }
    }

    private void OnExportLatest(object? sender, EventArgs e)
    {
        MessageBox.Show("请通过「续签/升级」重新颁发并导出最新证书。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenKeyManagement()
    {
        using var dlg = new KeyManagementForm(_keyMgmt);
        dlg.ShowDialog();
    }

    private void OpenIssuanceLog()
    {
        using var dlg = new IssuanceLogForm(_logService);
        dlg.ShowDialog();
    }

    private void SearchBox_TextChanged(object? sender, EventArgs e) => FilterCustomers();

    private void AddCustomerBtn_Click(object? sender, EventArgs e) => OnAddCustomer(sender, e);

    private void ToolsMenuKeyManagement_Click(object? sender, EventArgs e) => OpenKeyManagement();

    private void ToolsMenuIssuanceLog_Click(object? sender, EventArgs e) => OpenIssuanceLog();

    private void ToolsMenuExit_Click(object? sender, EventArgs e) => Application.Exit();
}
