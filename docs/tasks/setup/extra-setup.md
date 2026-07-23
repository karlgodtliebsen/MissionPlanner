# Setup Extra. Move embedded VerticalStackLayout sections in MandatoryHardwareView.xaml to dedicated Views

**Implementation status:** 

## Objective



## Repository constraints

- Work only under `src/`, `docs/`, `scripts/`, and test-data folders belonging to the new solution.
- Treat `src-v.1.38/` as read-only reference material. Never modify, format, move, or include legacy files in commits.
- Preserve the existing layered architecture: wire protocol in `MissionPlanner.MavLink`, transport in `MissionPlanner.Transport`, application/domain behavior in `MissionPlanner.Core`, and MAUI presentation in `MissionPlanner.App`.
- Do not call MAVLink transports directly from views or code-behind. Use application/domain services injected into view models.
- Keep code-behind limited to view lifecycle and unavoidable platform/UI integration.
- Use CommunityToolkit.Mvvm patterns already present in the solution.
- Reuse `VehicleId`, `VehicleSession`, `VehicleRegistry`, command ACK tracking, parameter services, domain event hub, generated MAVLink messages, and decoder catalog rather than creating parallel abstractions.
- All vehicle-changing operations must be connection-aware, cancellation-aware, target the active `VehicleId`, and expose command acknowledgement or an explicit failure state.
- Add structured logging at workflow boundaries; do not log high-frequency telemetry on every update.
- Add unit tests for domain/application behavior and view-model tests. Add smoke/integration tests only where they are deterministic.
- Update DI registrations in the existing configurators and add DI validation tests.
- The solution must build with nullable warnings treated consistently with the repository.
- In MAUI Views, Button must have these 2 properties added:  BackgroundColor="Transparent" FontSize="14" 

## Scope

In the View MandatoryHardwareView.xaml, a set of VerticalStackLayout holds xaml that should be moved to a dedicated View inherited from ContentView and added as a Control/Component into MandatoryHardwareView.



Sample:

This section:
                <VerticalStackLayout IsVisible="{Binding IsFirmwareSelected}" Spacing="10">
                    <Label Text="Connected identity" FontSize="18" FontAttributes="Bold" />
                    <Grid ColumnDefinitions="180,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto" RowSpacing="5">
                        <Label Text="Vehicle" FontAttributes="Bold" />
                        <Label Grid.Column="1" Text="{Binding SelectedFirmwareViewModel.VehicleLabel}" />
                        <Label Grid.Row="1" Text="Firmware" FontAttributes="Bold" />
                        <Label Grid.Row="1" Grid.Column="1" Text="{Binding SelectedFirmwareViewModel.FirmwareVersion}" />
                        <Label Grid.Row="2" Text="Release type / Git" FontAttributes="Bold" />
                        <Label Grid.Row="2" Grid.Column="1" Text="{Binding SelectedFirmwareViewModel.ReleaseType, StringFormat='{0}'}" />
                        <Label Grid.Row="3" Text="Git hash" FontAttributes="Bold" />
                        <Label Grid.Row="3" Grid.Column="1" Text="{Binding SelectedFirmwareViewModel.GitHash}" />
                        <Label Grid.Row="4" Text="Board version" FontAttributes="Bold" />
                        <Label Grid.Row="4" Grid.Column="1" Text="{Binding SelectedFirmwareViewModel.BoardVersion}" />
                        <Label Grid.Row="5" Text="Vendor / product" FontAttributes="Bold" />
                        <Label Grid.Row="5" Grid.Column="1" Text="{Binding SelectedFirmwareViewModel.VendorProduct}" />
                        <Label Grid.Row="6" Text="UID / UID2" FontAttributes="Bold" />
                        <VerticalStackLayout Grid.Row="6" Grid.Column="1">
                            <Label Text="{Binding SelectedFirmwareViewModel.HardwareUid}" />
                            <Label Text="{Binding SelectedFirmwareViewModel.HardwareUid2}" />
                        </VerticalStackLayout>
                        <Label Grid.Row="7" Text="MAVLink version" FontAttributes="Bold" />
                        <Label Grid.Row="7" Grid.Column="1" Text="{Binding SelectedFirmwareViewModel.MavLinkVersion}" />
                        <Label Grid.Row="8" Text="Capabilities" FontAttributes="Bold" />
                        <Label Grid.Row="8" Grid.Column="1" Text="{Binding SelectedFirmwareViewModel.Capabilities}" LineBreakMode="WordWrap" />
                    </Grid>

                    <Label Text="Firmware discovery" FontSize="18" FontAttributes="Bold" />
                    <HorizontalStackLayout Spacing="10">
                        <Picker Title="Channel"
                                    ItemsSource="{Binding SelectedFirmwareViewModel.Channels}"
                                    SelectedItem="{Binding SelectedFirmwareViewModel.SelectedChannel}" />
                        <Button BackgroundColor="Transparent" FontSize="14" Text="Discover compatible releases"
                                Command="{Binding SelectedFirmwareViewModel.DiscoverCommand}" />
                    </HorizontalStackLayout>
                    <Picker Title="Compatible release (exact board IDs)"
                                ItemsSource="{Binding SelectedFirmwareViewModel.Releases}"
                                SelectedItem="{Binding SelectedFirmwareViewModel.SelectedRelease}"
                                ItemDisplayBinding="{Binding Version}" />
                    <Label Text="{Binding SelectedFirmwareViewModel.SelectedBoardTarget, StringFormat='Technical board target: {0}'}" FontAttributes="Bold" />
                    <Label Text="{Binding SelectedFirmwareViewModel.SelectedReleaseNotes}" LineBreakMode="WordWrap" />
                    <HorizontalStackLayout Spacing="10">
                        <Button BackgroundColor="Transparent" FontSize="14" Text="Download and verify"
                                Command="{Binding SelectedFirmwareViewModel.DownloadCommand}" />
                        <Button BackgroundColor="Transparent" FontSize="14" Text="Flash verified package"
                                    Command="{Binding SelectedFirmwareViewModel.FlashCommand}" />
                    </HorizontalStackLayout>
                    <Label Text="{Binding SelectedFirmwareViewModel.Status}" FontAttributes="Italic" LineBreakMode="WordWrap" />
                    <Label Text="{Binding SelectedFirmwareViewModel.FlashingAvailability}" LineBreakMode="WordWrap" />
                    <Label Text="After flashing: reconnect, confirm the reported firmware identity, then restore only reviewed parameters from your backup." LineBreakMode="WordWrap" FontAttributes="Bold" />
                </VerticalStackLayout>


This section becomes the view FirmwareSetupView, and uses the corresponding FirmwareSetupViewModel


The view FirmwareSetupView is then added in MandatoryHardwareView as is standard for MAUI xaml views, like this:

		<views:FirmwareSetupView IsVisible="{Binding IsFirmwareSelected}" Spacing="10">

		</views:FirmwareSetupView>



The corresponding MandatoryHardwareViewModel must be adjusted to accommodate this, relevant Observable properties now belongs in the viewmodels, like in this sample, where Status is moved from MandatoryHardwareView to : 
FirmwareSetupView/FirmwareSetupViewModel:

                    <Label Text="{Binding Status}" FontAttributes="Italic" LineBreakMode="WordWrap" />




