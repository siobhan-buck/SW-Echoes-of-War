using SWLOR.Game.Server.Enumeration;
using SWLOR.Game.Server.Service;
using SWLOR.Game.Server.Service.GuiService;

namespace SWLOR.Game.Server.Feature.GuiDefinition.ViewModel
{
    public class DMAdminToolsViewModel : GuiViewModelBase<DMAdminToolsViewModel, GuiPayloadBase>
    {
        public const string MainContentElement = "dmadmintools_content";
        public const string MainContentPartial = "DMADMINTOOLS_MAIN";

        public bool CanManageBans
        {
            get => Get<bool>();
            set => Set(value);
        }

        public bool CanManageStaff
        {
            get => Get<bool>();
            set => Set(value);
        }

        protected override void Initialize(GuiPayloadBase initialPayload)
        {
            Geometry = DMToolbarViewModel.PlaceBelowToolbar(Player, Geometry);
            ChangePartialView(MainContentElement, MainContentPartial);
            AuthorizationLevel authorization = Authorization.GetAuthorizationLevel(Player);
            CanManageStaff = authorization == AuthorizationLevel.Admin;
            CanManageBans = CanManageStaff || authorization == AuthorizationLevel.DM;
        }

        public Action OnClickManageStaff() => () =>
        {
            if (CanManageStaff)
                DMPresence.ToggleWindow(Player, GuiWindowType.ManageStaff);
        };

        public Action OnClickManageBans() => () =>
        {
            if (CanManageBans)
                DMPresence.ToggleWindow(Player, GuiWindowType.ManageBans);
        };
    }
}
