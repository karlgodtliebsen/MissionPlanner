using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

public class FirmwareCatalogViewModel : ViewModelBase
{
    public FirmwareCatalogViewModel()
    {

    }
    public FirmwareCatalogViewModel(ILogger<FirmwareCatalogViewModel> logger) : base(logger)
    {
    }
}
