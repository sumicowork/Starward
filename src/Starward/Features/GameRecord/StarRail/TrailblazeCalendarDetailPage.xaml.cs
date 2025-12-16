using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Starward.Core.GameRecord;
using Starward.Core.GameRecord.StarRail.TrailblazeCalendar;
using Starward.Frameworks;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Features.GameRecord.StarRail;

public sealed partial class TrailblazeCalendarDetailPage : PageBase
{
    private readonly ILogger<TrailblazeCalendarDetailPage> _logger = AppConfig.GetLogger<TrailblazeCalendarDetailPage>();

    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();

    public TrailblazeCalendarDetailPage()
    {
        this.InitializeComponent();
    }

    private GameRecordRole gameRole;

    private int pageType = 1; // 1=Stellar Jade, 2=Pass (set from navigation parameter)

    public string PageTitle => pageType == 1
        ? Lang.TrailblazeCalendarPage_StellarJadeDetails
        : Lang.TrailblazeCalendarPage_PassDetails;

    public string IconPath => pageType == 1
        ? "ms-appx:///Assets/Image/900001.png"  // Stellar Jade icon
        : "ms-appx:///Assets/Image/101.png";     // Pass icon

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is ValueTuple<GameRecordRole, int> param)
        {
            gameRole = param.Item1;
            pageType = param.Item2;
            SelectedDetailType = pageType;
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(IconPath));
            OnPropertyChanged(nameof(IsStellarJadeSelected));
            OnPropertyChanged(nameof(IsPassSelected));
        }
        else if (e.Parameter is GameRecordRole role)
        {
            // Fallback for backward compatibility
            gameRole = role;
            pageType = 1;
            SelectedDetailType = 1;
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(IconPath));
            OnPropertyChanged(nameof(IsStellarJadeSelected));
            OnPropertyChanged(nameof(IsPassSelected));
        }
    }

    protected override async void OnLoaded()
    {
        await Task.Delay(16);
        await InitializeDataAsync();
    }

    protected override void OnUnloaded()
    {
        AllDetailItems = null!;
        FilteredDetailItems = null!;
        AvailableYears = null!;
        AvailableMonths = null!;
        ActionNames = null!;
    }

    [ObservableProperty]
    private List<TrailblazeCalendarDetailItem> allDetailItems;

    [ObservableProperty]
    private List<TrailblazeCalendarDetailItem> filteredDetailItems;

    [ObservableProperty]
    private List<string> availableYears;

    [ObservableProperty]
    private string? selectedYear;

    [ObservableProperty]
    private List<string> availableMonths;

    [ObservableProperty]
    private string? selectedMonth;

    [ObservableProperty]
    private List<string> actionNames;

    [ObservableProperty]
    private string? selectedActionName;

    [ObservableProperty]
    private int selectedDetailType = 1; // 1=Stellar Jade, 2=Pass

    [ObservableProperty]
    private int totalAmount;

    private bool _isUpdatingFilters = false;

    public bool IsStellarJadeSelected => SelectedDetailType == 1;
    
    public bool IsPassSelected => SelectedDetailType == 2;

    public int RecordCount => FilteredDetailItems?.Count ?? 0;

    public Visibility IsEmptyState => (FilteredDetailItems == null || FilteredDetailItems.Count == 0) ? Visibility.Visible : Visibility.Collapsed;


    private async Task InitializeDataAsync()
    {
        try
        {
            if (gameRole is null)
            {
                return;
            }

            await LoadAllDataAsync();
            InitializeFilters();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Error(ex);
        }
    }

    private async Task LoadAllDataAsync()
    {
        try
        {
            if (gameRole is null)
            {
                return;
            }

            // Get all month data from database
            var monthDataList = _gameRecordService.GetTrailblazeCalendarMonthDataList(gameRole);
            
            var allItems = new List<TrailblazeCalendarDetailItem>();
            
            foreach (var monthData in monthDataList)
            {
                var itemsJade = _gameRecordService.GetTrailblazeCalendarDetailItems(monthData.Uid, monthData.Month, 1);
                var itemsPass = _gameRecordService.GetTrailblazeCalendarDetailItems(monthData.Uid, monthData.Month, 2);
                allItems.AddRange(itemsJade);
                allItems.AddRange(itemsPass);
            }

            AllDetailItems = allItems.OrderByDescending(x => x.Time).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load all data");
            AllDetailItems = new List<TrailblazeCalendarDetailItem>();
        }
    }

    private void InitializeFilters()
    {
        try
        {
            _isUpdatingFilters = true;
            
            if (AllDetailItems == null || AllDetailItems.Count == 0)
            {
                AvailableYears = new List<string> { Lang.TrailblazeCalendarPage_AllYears };
                AvailableMonths = new List<string> { Lang.TrailblazeCalendarPage_AllMonths };
                ActionNames = new List<string> { Lang.TrailblazeCalendarPage_AllNames };
                SelectedYear = AvailableYears.FirstOrDefault();
                SelectedMonth = AvailableMonths.FirstOrDefault();
                SelectedActionName = ActionNames.FirstOrDefault();
                return;
            }

            // Get unique years
            var years = AllDetailItems
                .Select(x => x.Time.Year.ToString())
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();
            AvailableYears = new List<string> { Lang.TrailblazeCalendarPage_AllYears };
            AvailableYears.AddRange(years);
            SelectedYear = AvailableYears.FirstOrDefault();

            UpdateMonthsAndNames();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize filters");
        }
        finally
        {
            _isUpdatingFilters = false;
        }
    }

    partial void OnSelectedYearChanged(string? value)
    {
        try
        {
            if (_isUpdatingFilters) return;
            
            // Defensive check: if value is not in AvailableYears, ignore this change
            if (!string.IsNullOrEmpty(value) && AvailableYears != null && !AvailableYears.Contains(value))
            {
                return;
            }
            
            _isUpdatingFilters = true;
            UpdateMonthsAndNames();
            ApplyFilters();
        }
        catch (System.Runtime.InteropServices.COMException comEx)
        {
            _logger.LogError("COM EXCEPTION in OnSelectedYearChanged! HRESULT: 0x{HResult:X8}, Message: {Message}", comEx.HResult, comEx.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in OnSelectedYearChanged");
            throw;
        }
        finally
        {
            _isUpdatingFilters = false;
        }
    }

    partial void OnSelectedMonthChanged(string? value)
    {
        try
        {
            if (_isUpdatingFilters) return;
            
            // Defensive check: if value is not in AvailableMonths, ignore this change
            if (!string.IsNullOrEmpty(value) && AvailableMonths != null && !AvailableMonths.Contains(value))
            {
                return;
            }
            
            _isUpdatingFilters = true;
            UpdateActionNames();
            ApplyFilters();
        }
        catch (System.Runtime.InteropServices.COMException comEx)
        {
            _logger.LogError("COM EXCEPTION in OnSelectedMonthChanged! HRESULT: 0x{HResult:X8}, Message: {Message}, SelectedMonth: {Month}", 
                comEx.HResult, comEx.Message, SelectedMonth);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in OnSelectedMonthChanged");
            throw;
        }
        finally
        {
            _isUpdatingFilters = false;
        }
    }

    partial void OnSelectedActionNameChanged(string? value)
    {
        if (_isUpdatingFilters) return;
        
        // Defensive check: if value is not in ActionNames, ignore this change
        if (!string.IsNullOrEmpty(value) && ActionNames != null && !ActionNames.Contains(value))
        {
            return;
        }
        
        ApplyFilters();
    }

    private void TypeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingFilters) return;
        
        if (sender is ToggleButton toggleButton && toggleButton.Tag is string tagStr && int.TryParse(tagStr, out int type))
        {
            // Prevent unchecking - at least one must be selected
            if (toggleButton.IsChecked == false && SelectedDetailType == type)
            {
                toggleButton.IsChecked = true;
                return;
            }
            
            SelectedDetailType = type;
            OnPropertyChanged(nameof(IsStellarJadeSelected));
            OnPropertyChanged(nameof(IsPassSelected));
            
            _isUpdatingFilters = true;
            try
            {
                UpdateMonthsAndNames();
                ApplyFilters();
            }
            finally
            {
                _isUpdatingFilters = false;
            }
        }
    }

    private void UpdateMonthsAndNames()
    {
        try
        {
            // CRITICAL: Clear SelectedMonth BEFORE updating AvailableMonths
            var oldSelection = SelectedMonth;
            SelectedMonth = null;
            
            if (AllDetailItems == null || AllDetailItems.Count == 0)
            {
                AvailableMonths = new List<string> { Lang.TrailblazeCalendarPage_AllMonths };
                SelectedMonth = AvailableMonths.FirstOrDefault();
                UpdateActionNames();
                return;
            }

            var items = AllDetailItems.AsEnumerable();
            
            // Filter by type
            items = items.Where(x => x.Type == SelectedDetailType);

            // Filter by year if not "All"
            if (!string.IsNullOrEmpty(SelectedYear) && SelectedYear != Lang.TrailblazeCalendarPage_AllYears)
            {
                items = items.Where(x => x.Time.Year.ToString() == SelectedYear);
            }

            // Get available months
            var itemsList = items.ToList();
            if (itemsList.Count == 0)
            {
                AvailableMonths = new List<string> { Lang.TrailblazeCalendarPage_AllMonths };
                SelectedMonth = AvailableMonths.FirstOrDefault();
                UpdateActionNames();
                return;
            }

            var months = itemsList
                .Select(x => x.Time.ToString("MM"))
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

            // Build new list
            var newMonths = new List<string> { Lang.TrailblazeCalendarPage_AllMonths };
            if (months.Count > 0)
            {
                newMonths.AddRange(months.Select(m => $"{int.Parse(m)}月"));
            }
            
            // Update ItemsSource only after SelectedItem is null
            try
            {
                AvailableMonths = newMonths;
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                _logger.LogError("COM EXCEPTION when updating AvailableMonths! HRESULT: 0x{HResult:X8}, SelectedMonth: {Month}", 
                    comEx.HResult, SelectedMonth);
                throw;
            }
            
            // Try to restore previous selection if it's still valid
            try
            {
                if (!string.IsNullOrEmpty(oldSelection) && newMonths.Contains(oldSelection))
                {
                    SelectedMonth = oldSelection;
                }
                else
                {
                    SelectedMonth = AvailableMonths.FirstOrDefault();
                }
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                _logger.LogError("COM EXCEPTION when setting SelectedMonth! HRESULT: 0x{HResult:X8}", comEx.HResult);
                throw;
            }
            
            UpdateActionNames();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateMonthsAndNames");
            SelectedMonth = null;
            AvailableMonths = new List<string> { Lang.TrailblazeCalendarPage_AllMonths };
            SelectedMonth = AvailableMonths.FirstOrDefault();
            UpdateActionNames();
        }
    }

    private void UpdateActionNames()
    {
        try
        {
            // CRITICAL: Clear SelectedActionName BEFORE updating ActionNames
            var oldSelection = SelectedActionName;
            SelectedActionName = null;
            
            if (AllDetailItems == null || AllDetailItems.Count == 0)
            {
                ActionNames = new List<string> { Lang.TrailblazeCalendarPage_AllNames };
                SelectedActionName = ActionNames.FirstOrDefault();
                return;
            }

            var items = AllDetailItems.AsEnumerable();
            
            // Filter by type
            items = items.Where(x => x.Type == SelectedDetailType);

            // Filter by year
            if (!string.IsNullOrEmpty(SelectedYear) && SelectedYear != Lang.TrailblazeCalendarPage_AllYears)
            {
                items = items.Where(x => x.Time.Year.ToString() == SelectedYear);
            }

            // Filter by month
            if (!string.IsNullOrEmpty(SelectedMonth) && SelectedMonth != Lang.TrailblazeCalendarPage_AllMonths)
            {
                var monthNum = SelectedMonth.Replace("月", "");
                if (int.TryParse(monthNum, out int month))
                {
                    items = items.Where(x => x.Time.Month == month);
                }
            }

            // Get unique action names
            var itemsList = items.ToList();
            var newNames = new List<string> { Lang.TrailblazeCalendarPage_AllNames };
            if (itemsList.Count > 0)
            {
                var names = itemsList
                    .Select(x => x.ActionName)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                if (names.Count > 0)
                {
                    newNames.AddRange(names);
                }
            }

            // Update ItemsSource only after SelectedItem is null
            try
            {
                ActionNames = newNames;
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                _logger.LogError("COM EXCEPTION when updating ActionNames! HRESULT: 0x{HResult:X8}", comEx.HResult);
                throw;
            }
            
            // Try to restore previous selection if it's still valid
            try
            {
                if (!string.IsNullOrEmpty(oldSelection) && newNames.Contains(oldSelection))
                {
                    SelectedActionName = oldSelection;
                }
                else
                {
                    SelectedActionName = ActionNames.FirstOrDefault();
                }
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                _logger.LogError("COM EXCEPTION when setting SelectedActionName! HRESULT: 0x{HResult:X8}", comEx.HResult);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateActionNames");
            SelectedActionName = null;
            ActionNames = new List<string> { Lang.TrailblazeCalendarPage_AllNames };
            SelectedActionName = ActionNames.FirstOrDefault();
        }
    }

    private void ApplyFilters()
    {
        try
        {
            if (AllDetailItems == null || AllDetailItems.Count == 0)
            {
                FilteredDetailItems = new List<TrailblazeCalendarDetailItem>();
                TotalAmount = 0;
                OnPropertyChanged(nameof(RecordCount));
                OnPropertyChanged(nameof(IsEmptyState));
                return;
            }

            var items = AllDetailItems.AsEnumerable();

            // Filter by type (1=Stellar Jade, 2=Pass)
            items = items.Where(x => x.Type == SelectedDetailType);

            // Filter by year
            if (!string.IsNullOrEmpty(SelectedYear) && SelectedYear != Lang.TrailblazeCalendarPage_AllYears)
            {
                items = items.Where(x => x.Time.Year.ToString() == SelectedYear);
            }

            // Filter by month
            if (!string.IsNullOrEmpty(SelectedMonth) && SelectedMonth != Lang.TrailblazeCalendarPage_AllMonths)
            {
                var monthNum = SelectedMonth.Replace("月", "");
                if (int.TryParse(monthNum, out int month))
                {
                    items = items.Where(x => x.Time.Month == month);
                }
            }

            // Filter by action name
            if (!string.IsNullOrEmpty(SelectedActionName) && SelectedActionName != Lang.TrailblazeCalendarPage_AllNames)
            {
                items = items.Where(x => x.ActionName == SelectedActionName);
            }

            FilteredDetailItems = items.ToList();
            TotalAmount = FilteredDetailItems.Sum(x => x.Number);
            OnPropertyChanged(nameof(RecordCount));
            OnPropertyChanged(nameof(IsEmptyState));
            
            // Update chart data
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in ApplyFilters");
            FilteredDetailItems = new List<TrailblazeCalendarDetailItem>();
            TotalAmount = 0;
            OnPropertyChanged(nameof(RecordCount));
            OnPropertyChanged(nameof(IsEmptyState));
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Frame != null && Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Go back");
        }
    }
}

