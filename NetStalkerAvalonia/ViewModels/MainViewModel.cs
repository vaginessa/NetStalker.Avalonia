using Avalonia;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using NetStalkerAvalonia.Compairers;
using NetStalkerAvalonia.Components.DeviceList;
using NetStalkerAvalonia.Configuration;
using NetStalkerAvalonia.Helpers;
using NetStalkerAvalonia.Models;
using NetStalkerAvalonia.Services;
using NetStalkerAvalonia.ViewModels.InteractionViewModels;
using NetStalkerAvalonia.ViewModels.RoutedViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace NetStalkerAvalonia.ViewModels
{
	public class MainViewModel : ViewModelBase, IDisposable
	{
		#region Subscriptions

		private IDisposable? _statusMessagesListener;
		private IDisposable? _blockAllFutureHandlerSubscription;
		private IDisposable? _redirectAllFutureHandlerSubscription;

		#endregion

		#region Members

		private bool _blockAllHandlerAttached;
		private bool _redirectAllHandlerAttached;

		#endregion

		#region Services

		// Required services
		private readonly IRouter _router;
		private IDeviceScanner _scanner;
		private IBlockerRedirector _blockerRedirector;
		private IDeviceNameResolver _deviceNameResolver;

		#endregion

		#region ViewModels

		private readonly SnifferViewModel _snifferViewModel;
		private readonly OptionsViewModel _optionsViewModel;
		private readonly RuleBuilderViewModel _ruleBuilderViewModel;
		private readonly HelpViewModel _helpViewModel;
		private readonly AboutViewModel _aboutViewModel;

		public readonly AppLogViewModel _appLogViewModel;

		#endregion

		#region Routing

		// The Router associated with this Screen.
		// Required by the IScreen interface.
		public RoutingState Router => _router.Router;

		// Commands to navigate the different views
		public ReactiveCommand<Unit, IRoutableViewModel> GoToRules { get; }
		public ReactiveCommand<Unit, IRoutableViewModel> GoToSniffer { get; }
		public ReactiveCommand<Unit, IRoutableViewModel> GoToOptions { get; }
		public ReactiveCommand<Unit, IRoutableViewModel> GoToHelp { get; }
		public ReactiveCommand<Unit, IRoutableViewModel> GoToAbout { get; }

		// The command that navigates a user back.
		public ReactiveCommand<Unit, Unit> GoBack => Router.NavigateBack;
		public void GoBackHandler() => GoBack?.Execute();

		private readonly ObservableAsPropertyHelper<bool> _canGoBack;
		public bool CanGoBack => _canGoBack.Value;

		#endregion

		#region Nav commands

		public ReactiveCommand<Unit, Unit>? Scan { get; }
		public ReactiveCommand<Unit, Unit>? Refresh { get; }
		public ReactiveCommand<bool, Unit>? BlockAll { get; }
		public ReactiveCommand<bool, Unit>? RedirectAll { get; }
		public ReactiveCommand<Unit, Unit>? ShowAppLog { get; }

		#endregion

		#region Context Menu Commands

		public ReactiveCommand<PhysicalAddress?, Unit> BlockUnblock { get; }
		public ReactiveCommand<PhysicalAddress?, Unit> RedirectUnRedirect { get; }
		public ReactiveCommand<PhysicalAddress?, Unit> Limit { get; }
		public ReactiveCommand<PhysicalAddress?, Unit> SetFriendlyName { get; }
		public ReactiveCommand<PhysicalAddress?, Unit> ClearFriendlyName { get; }

		#endregion

		#region Tray Icon

		private bool _trayIconVisible;
		public bool TrayIconVisible
		{
			get => _trayIconVisible;
			set => this.RaiseAndSetIfChanged(ref _trayIconVisible, value);
		}

		public ReactiveCommand<Unit, Unit>? ShowApp { get; }
		public ReactiveCommand<Unit, Unit>? ExitApp { get; }

		public void InitTrayIcon()
		{
			StaticData.MainWindow!.PropertyChanged += MainWindow_PropertyChanged;
		}

		private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property.Name == nameof(Window.WindowState))
			{
				var state = (WindowState)e.NewValue!;

				if (state == WindowState.Minimized)
				{
					StaticData.MainWindow!.ShowInTaskbar = Config.AppSettings!.MinimizeToTraySetting == false;
					TrayIconVisible = Config.AppSettings!.MinimizeToTraySetting;
				}
				else
				{
					StaticData.MainWindow!.ShowInTaskbar = true;
				}
			}
		}

		#endregion

		#region Interactions

		public Interaction<DeviceLimitsModel?, DeviceLimitsModel?> ShowLimitDialogInteraction { get; set; }
		public Interaction<StatusMessageModel, Unit> ShowStatusMessageInteraction { get; set; }
		public Interaction<string?, string?> SetFriendlyNameInteraction { get; set; }
		public Interaction<Unit, Unit> ShowAppLogInteraction { get; set; }

		#endregion

		#region UI Bounded Properties

		private Device? _selectedDevice;

		public Device? SelectedDevice
		{
			get => _selectedDevice;
			set => this.RaiseAndSetIfChanged(ref _selectedDevice, value);
		}

		private bool _allBlocked;

		public bool AllBlocked
		{
			get => _allBlocked;
			set => this.RaiseAndSetIfChanged(ref _allBlocked, value);
		}

		private bool _allRedirected;

		public bool AllRedirected
		{
			get => _allRedirected;
			set => this.RaiseAndSetIfChanged(ref _allRedirected, value);
		}

		private bool _scanEnabled;

		public bool ScanEnabled
		{
			get => _scanEnabled;
			set => this.RaiseAndSetIfChanged(ref _scanEnabled, value);
		}

		private readonly ObservableAsPropertyHelper<string> _pageTitle;
		public string PageTitle => _pageTitle.Value;

		#endregion

		#region Devices List

		// Accessor to expose the UI device list
		public ReadOnlyObservableCollection<Device>? Devices => _blockerRedirector?.Devices;

		// Configure the device list view
		public DeviceListViewSettings DeviceListViewSettings { get; set; } = new();

		#endregion

		#region Helpers

		private readonly IEqualityComparer<Device> _deviceEqualityComparer = new DeviceEqualityComparer();

		#endregion

		#region Constructor

#if DEBUG

		public MainViewModel()
		{

		}

#endif

		[Splat.DependencyInjectionConstructor]
		public MainViewModel(
			IRouter router,
			IDeviceScanner deviceScanner,
			IBlockerRedirector blockerRedirector,
			IDeviceNameResolver deviceNameResolver,
			SnifferViewModel snifferViewModel,
			OptionsViewModel optionsViewModel,
			RuleBuilderViewModel ruleBuilderViewModel,
			HelpViewModel helpViewModel,
			AboutViewModel aboutViewModel,
			AppLogViewModel appLogViewModel)
		{
			_router = router;
			_scanner = deviceScanner;
			_blockerRedirector = blockerRedirector;
			_deviceNameResolver = deviceNameResolver;
			_snifferViewModel = snifferViewModel;
			_optionsViewModel = optionsViewModel;
			_ruleBuilderViewModel = ruleBuilderViewModel;
			_helpViewModel = helpViewModel;
			_aboutViewModel = aboutViewModel;
			_appLogViewModel = appLogViewModel;

			#region Page info wiring

			// Info wiring
			_pageTitle = this.WhenAnyObservable(x => x.Router.CurrentViewModel)
				.Select(GetPageNameFromViewModel)
				.ToProperty(this, x => x.PageTitle);

			ScanEnabled = true;

			#endregion

			#region Navigation wiring

			_canGoBack = this.WhenAnyValue(x => x.Router.NavigationStack.Count)
				.Select(count => count > 0)
				.ToProperty(this, x => x.CanGoBack);

			GoToSniffer = ReactiveCommand.CreateFromObservable(
				() => Router.Navigate.Execute(snifferViewModel));

			GoToOptions = ReactiveCommand.CreateFromObservable(
				() => Router.Navigate.Execute(optionsViewModel));

			GoToRules = ReactiveCommand.CreateFromObservable(
				() => Router.Navigate.Execute(ruleBuilderViewModel));

			GoToHelp = ReactiveCommand.CreateFromObservable(
				() => Router.Navigate.Execute(helpViewModel));

			GoToAbout = ReactiveCommand.CreateFromObservable(
				() => Router.Navigate.Execute(aboutViewModel));

			#endregion

			#region Nav Command wiring

			Scan = ReactiveCommand.Create(ScanImpl);
			Refresh = ReactiveCommand.Create(RefreshImpl);
			BlockAll = ReactiveCommand.CreateFromTask<bool>(BlockAllImpl);
			RedirectAll = ReactiveCommand.CreateFromTask<bool>(RedirectAllImpl);
			ShowAppLog = ReactiveCommand.CreateFromTask(ShowAppLogImpl);

			#endregion

			#region Context Menu command wiring

			BlockUnblock = ReactiveCommand.CreateFromTask<PhysicalAddress?>(BlockUnblockImpl);
			RedirectUnRedirect = ReactiveCommand.CreateFromTask<PhysicalAddress?>(RedirectUnRedirectImpl);

			#region Limit Dialog

			ShowLimitDialogInteraction = new Interaction<DeviceLimitsModel?, DeviceLimitsModel?>();
			Limit = ReactiveCommand.CreateFromTask<PhysicalAddress?>(LimitImpl);

			#endregion

			#region Friendly Target Name

			SetFriendlyNameInteraction = new Interaction<string?, string?>();
			SetFriendlyName = ReactiveCommand.CreateFromTask<PhysicalAddress?>(SetFriendlyNameImpl);
			ClearFriendlyName = ReactiveCommand.CreateFromTask<PhysicalAddress?>(ClearFriendlyNameImpl);

			#endregion

			#endregion

			#region Status message

			ShowStatusMessageInteraction = new Interaction<StatusMessageModel, Unit>();

			// This message bus listener is used for displaying status messages by other components in the app
			_statusMessagesListener = MessageBus
					.Current
					.Listen<StatusMessageModel>(ContractKeys.StatusMessage.ToString())
					.ObserveOn(RxApp.MainThreadScheduler)
					.Select(x => ShowStatusMessage(x))
					.Subscribe();

			#endregion

			#region Exception Handling

			Scan.ThrownExceptions.Subscribe(x =>
				Tools.HandleError(new StatusMessageModel(MessageType.Error, x.Message)));
			Refresh.ThrownExceptions.Subscribe(x =>
				Tools.HandleError(new StatusMessageModel(MessageType.Error, x.Message)));
			BlockUnblock.ThrownExceptions.Subscribe(x =>
				Tools.HandleError(new StatusMessageModel(MessageType.Error, x.Message)));
			RedirectUnRedirect.ThrownExceptions.Subscribe(x =>
				Tools.HandleError(new StatusMessageModel(MessageType.Error, x.Message)));
			BlockAll.ThrownExceptions.Subscribe(x =>
				Tools.HandleError(new StatusMessageModel(MessageType.Error, x.Message)));
			RedirectAll.ThrownExceptions.Subscribe(x =>
				Tools.HandleError(new StatusMessageModel(MessageType.Error, x.Message)));

			#endregion

			#region Tray Icon			

			ShowApp = ReactiveCommand.Create(Tools.ShowApp);
			ExitApp = ReactiveCommand.Create(Tools.ExitApp);

			#endregion

			#region App Log

			ShowAppLogInteraction = new Interaction<Unit, Unit>();

			#endregion
		}

		#endregion

		#region Command Handlers

		private void ScanImpl()
		{
			_scanner?.Scan();

			ScanEnabled = false;
		}

		private void RefreshImpl()
		{
			// Send a round of ARP packets to discover devices quicker
			Task.Run(() => _scanner?.Refresh());
		}

		private async Task BlockUnblockImpl(PhysicalAddress? mac)
		{
			(bool isValid, Device device) = await CheckIfMacAddressIsValidAsync(mac);

			if (isValid == false)
				return;

			if (device.Blocked == false)
			{
				_blockerRedirector?
					.Block(device);
			}
			else
			{
				_blockerRedirector?
					.UnBlock(device);
			}
		}

		private async Task RedirectUnRedirectImpl(PhysicalAddress? mac)
		{
			(bool isValid, Device device) = await CheckIfMacAddressIsValidAsync(mac);

			if (isValid == false)
				return;

			if (device.Redirected == false)
			{
				_blockerRedirector?.Redirect(device);
			}
			else
			{
				_blockerRedirector?.UnRedirect(device);
			}
		}

		private async Task LimitImpl(PhysicalAddress? mac)
		{
			(bool isValid, Device device) = await CheckIfMacAddressIsValidAsync(mac);

			if (isValid == false)
				return;

			var result =
				await ShowLimitDialogInteraction!.Handle(new DeviceLimitsModel(device.DownloadCap / 1024,
					device.UploadCap / 1024));

			if (result != null)
			{
				_blockerRedirector?.Limit(device!, result.Download, result.Upload);
			}
		}

		private async Task BlockAllImpl(bool active)
		{
			if (AllRedirected && active)
			{
				AllBlocked = false;

				await ShowStatusMessageInteraction
					.Handle(
						new StatusMessageModel(MessageType.Error,
							"You have to uncheck the Redirect All toggle first!"));
				return;
			}

			if (active)
			{
				var devices = Devices!
					.Where(d => d.IsGateway() == false && d.IsLocalDevice() == false & d.Blocked == false)
					.ToList();

				foreach (var device in devices)
				{
					_blockerRedirector?.Block(device);
				}

				// Attach handler to block all future detections
				if (_blockAllHandlerAttached == false)
				{
					_blockAllHandlerAttached = true;
					AttachBlockAllFutureDetectionsHandler();
				}
			}
			else
			{
				var devices = Devices!
					.Where(d => d.IsGateway() == false && d.IsLocalDevice() == false & d.Blocked == true)
					.ToList();

				foreach (var device in devices)
				{
					_blockerRedirector?.UnBlock(device);
				}

				// Remove handler to block all future detections
				if (_blockAllHandlerAttached == true)
				{
					_blockAllHandlerAttached = false;
					RemoveBlockAllFutureDetectionsHandler();
				}
			}

			AllBlocked = active;
		}

		private async Task RedirectAllImpl(bool active)
		{
			if (AllBlocked && active)
			{
				AllRedirected = false;

				await ShowStatusMessageInteraction
					.Handle(
						new StatusMessageModel(MessageType.Error,
							"You have to uncheck the Block All toggle first!"));
				return;
			}

			if (active)
			{
				var devices = Devices!
					.Where(d => d.IsGateway() == false && d.IsLocalDevice() == false && d.Redirected == false)
					.ToList();

				foreach (var device in devices)
				{
					_blockerRedirector?.Redirect(device);
				}

				// Attach handler to redirect all future detections
				if (_redirectAllHandlerAttached == false)
				{
					_redirectAllHandlerAttached = true;
					AttachRedirectAllFutureDetectionsHandler();
				}
			}
			else
			{
				var devices = Devices!
					.Where(d => d.IsGateway() == false && d.IsLocalDevice() == false && d.Redirected == true)
					.ToList();

				foreach (var device in devices)
				{
					_blockerRedirector?.UnRedirect(device);
				}

				// Remove handler to redirect all future detections
				if (_redirectAllHandlerAttached == true)
				{
					_redirectAllHandlerAttached = false;
					RemoveRedirectAllFutureDetectionsHandler();
				}
			}

			AllRedirected = active;
		}

		private async Task SetFriendlyNameImpl(PhysicalAddress? mac)
		{
			(bool isValid, Device device) = await CheckIfMacAddressIsValidAsync(mac, true);

			if (isValid == false)
				return;

			var result =
				await SetFriendlyNameInteraction!.Handle(device.Name);

			if (string.IsNullOrWhiteSpace(result) == false)
			{
				device.SetFriendlyName(result);
				_deviceNameResolver?.SaveDeviceNamesAsync(Devices!.ToList());
			}
		}

		private async Task ClearFriendlyNameImpl(PhysicalAddress? mac)
		{
			(bool isValid, Device device) = await CheckIfMacAddressIsValidAsync(mac, true);

			if (isValid == false)
				return;

			// It doesn't matter if we specify the second optional parameter or not
			device.SetFriendlyName(null!);

			_deviceNameResolver?.SaveDeviceNamesAsync(Devices!.ToList());
		}

		private async Task ShowAppLogImpl()
		{
			await ShowAppLogInteraction.Handle(Unit.Default);
		}

		#endregion

		#region Tools

		//We get the view name, otherwise we return the initial name
		private static string GetPageNameFromViewModel(IRoutableViewModel? routableViewModel)
		{
			return routableViewModel?.UrlPathSegment ?? "Device List";
		}

		// canBeAppliedToGatewayAndLocal parameter means friendly commands like setting device name, clear name, etc.
		private async Task<(bool isValid, Device device)> CheckIfMacAddressIsValidAsync(PhysicalAddress? mac,
			bool canBeAppliedToGatewayAndLocal = false)
		{
			var device = Devices.FirstOrDefault(x => x.Mac!.Equals(mac));

			if (device == null)
			{
				await ShowStatusMessageInteraction!.Handle(new StatusMessageModel(MessageType.Error,
					"No device is selected!"));

				return (false, null!);
			}
			else if (device!.IsGateway() && canBeAppliedToGatewayAndLocal == false)
			{
				await ShowStatusMessageInteraction!.Handle(new StatusMessageModel(MessageType.Error,
					"Gateway can't be targeted!"));

				return (false, null!);
			}
			else if (device!.IsLocalDevice() && canBeAppliedToGatewayAndLocal == false)
			{
				await ShowStatusMessageInteraction!.Handle(new StatusMessageModel(MessageType.Error,
					"You can't target your own device!"));

				return (false, null!);
			}

			return (true, device);
		}

		public IEnumerable<Device> GetUiDeviceCollection() => Devices;

		private void AttachBlockAllFutureDetectionsHandler()
		{
			_blockAllFutureHandlerSubscription =
				Devices
					.ToObservableChangeSet()
					.Where(change => change.Adds > 0)
					.ToCollection()
					.Select(x => BlockAllImpl(true))
					.Subscribe();
		}

		private void RemoveBlockAllFutureDetectionsHandler()
		{
			_blockAllFutureHandlerSubscription?.Dispose();
		}

		private void AttachRedirectAllFutureDetectionsHandler()
		{
			_redirectAllFutureHandlerSubscription =
				Devices
					.ToObservableChangeSet()
					.Where(change => change.Adds > 0)
					.ToCollection()
					.Select(x => RedirectAllImpl(true))
					.Subscribe();
		}

		private void RemoveRedirectAllFutureDetectionsHandler()
		{
			_redirectAllFutureHandlerSubscription?.Dispose();
		}

		private async Task<Unit> ShowStatusMessage(StatusMessageModel statusMessage)
		{
			await ShowStatusMessageInteraction.Handle(statusMessage);
			return Unit.Default;
		}

		#endregion

		public void Dispose()
		{
			_statusMessagesListener?.Dispose();
			RemoveBlockAllFutureDetectionsHandler();
			RemoveRedirectAllFutureDetectionsHandler();
		}
	}
}