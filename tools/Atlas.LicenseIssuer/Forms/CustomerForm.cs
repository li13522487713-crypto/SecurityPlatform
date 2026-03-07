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
            _remarkBox.Text = existing.Remark ?? "";
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
