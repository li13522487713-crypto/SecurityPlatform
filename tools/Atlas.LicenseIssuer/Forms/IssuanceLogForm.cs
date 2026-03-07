using Atlas.LicenseIssuer.Services;

namespace Atlas.LicenseIssuer.Forms;

public sealed partial class IssuanceLogForm : Form
{
    private readonly IssuanceLogService _logService;

    public IssuanceLogForm(IssuanceLogService logService)
    {
        _logService = logService;
        InitializeComponent();
        LoadData();
    }

    private void LoadData()
    {
        var logs = _logService.GetAll(200);
        _grid.DataSource = logs;
    }

    private void RefreshBtn_Click(object? sender, EventArgs e) => LoadData();
}
