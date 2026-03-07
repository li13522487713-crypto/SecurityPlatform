using Atlas.LicenseIssuer.Models;
using Atlas.LicenseIssuer.Services;

namespace Atlas.LicenseIssuer.Forms;

public sealed partial class CustomerForm : Form
{
    private readonly CustomerService _customerService;
    private readonly CustomerRecord? _existing;

    public CustomerRecord? Result { get; private set; }

    public CustomerForm(CustomerService customerService, CustomerRecord? existing = null)
    {
        _customerService = customerService;
        _existing = existing;
        InitializeComponent();
        if (existing is not null)
        {
            _nameBox.Text = existing.Name;
            _contactBox.Text = existing.Contact ?? "";
            _tenantIdBox.Text = existing.TenantId ?? "";
            _remarkBox.Text = existing.Remark ?? "";
        }
        else
        {
            // 新建客户时自动生成平台租户 GUID，操作员可在必要时手动覆盖
            _tenantIdBox.Text = Guid.NewGuid().ToString();
        }
    }

    private void SaveBtn_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show("请输入客户名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var record = _existing ?? new CustomerRecord();
        record.Name = _nameBox.Text.Trim();
        record.Contact = _contactBox.Text.Trim();
        record.TenantId = string.IsNullOrWhiteSpace(_tenantIdBox.Text) ? null : _tenantIdBox.Text.Trim();
        record.Remark = _remarkBox.Text.Trim();

        if (_existing is null)
            _customerService.Add(record);
        else
            _customerService.Update(record);

        Result = record;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelBtn_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
