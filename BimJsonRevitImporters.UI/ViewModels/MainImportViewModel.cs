using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BimJsonRevitImporter.Domain.BimJson;
using BimJsonRevitImporter.Domain.Messaging;
using BimJsonRevitImporters.UI.Helpers;
using Microsoft.Win32;

namespace BimJsonRevitImporters.UI.ViewModels
{
    public class MainImportViewModel : ObservableObject
    {
        private readonly IRevitEventDispatcher _dispatcher;
        private string _bimJsonPath;
        private string _statusText;
        private LevelInfo _selectedBaseLevel;
        private LevelInfo _selectedTopLevel;
        private CadInstanceInfo _selectedCadInstance;
        private bool _isBusy;

        public MainImportViewModel(IRevitEventDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            ReportLines = new ObservableCollection<string>();
            Levels = new ObservableCollection<LevelInfo>();
            WallTypes = new ObservableCollection<WallTypeInfo>();
            WallGroupRows = new ObservableCollection<WallGroupRow>();
            CadInstances = new ObservableCollection<CadInstanceInfo>();

            LoadBimJsonCommand = new RelayCommand(LoadBimJson, () => !IsBusy);
            PickCadCommand = new RelayCommand(PickCad, () => !IsBusy && _dispatcher != null);
            RefreshRevitDataCommand = new RelayCommand(RefreshRevitDataAsync, () => !IsBusy && _dispatcher != null);
            ImportWallsCommand = new RelayCommand(ImportWallsAsync, () => !IsBusy && CanImportWalls());

            if (_dispatcher != null)
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(RefreshRevitDataAsync), System.Windows.Threading.DispatcherPriority.Background);
        }

        public ObservableCollection<string> ReportLines { get; }
        public ObservableCollection<LevelInfo> Levels { get; }
        public ObservableCollection<WallTypeInfo> WallTypes { get; }
        public ObservableCollection<WallGroupRow> WallGroupRows { get; }
        public ObservableCollection<CadInstanceInfo> CadInstances { get; }

        public string BimJsonPath
        {
            get => _bimJsonPath;
            set => SetProperty(ref _bimJsonPath, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public LevelInfo SelectedBaseLevel
        {
            get => _selectedBaseLevel;
            set => SetProperty(ref _selectedBaseLevel, value);
        }

        public LevelInfo SelectedTopLevel
        {
            get => _selectedTopLevel;
            set => SetProperty(ref _selectedTopLevel, value);
        }

        public CadInstanceInfo SelectedCadInstance
        {
            get => _selectedCadInstance;
            set => SetProperty(ref _selectedCadInstance, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand LoadBimJsonCommand { get; }
        public ICommand PickCadCommand { get; }
        public ICommand RefreshRevitDataCommand { get; }
        public ICommand ImportWallsCommand { get; }

        private void LoadBimJson()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "BIMJSON / GeoJSON|*.json;*.geojson;*.bimjson|All files|*.*",
                Title = "Load BIMJSON…"
            };
            if (dlg.ShowDialog() != true) return;

            BimJsonPath = dlg.FileName;
            var walls = BimJsonReader.ReadWalls(BimJsonPath);
            var groups = BimJsonReader.GroupWallsByThickness(walls);

            WallGroupRows.Clear();
            foreach (var g in groups.OrderByDescending(x => x.Count))
            {
                var row = new WallGroupRow { ThicknessCm = g.ThicknessCm, Count = g.Count };
                if (WallTypes.Count > 0)
                    row.SelectedWallType = WallTypes[0];
                WallGroupRows.Add(row);
            }
            StatusText = $"Loaded {walls.Count} walls, {groups.Count} thickness groups.";
        }

        private async void PickCad()
        {
            if (_dispatcher == null) return;
            IsBusy = true;
            try
            {
                var request = new RevitRequest
                {
                    Type = RevitRequestType.PickCadInstance,
                    CorrelationId = Guid.NewGuid(),
                    Payload = null
                };
                var response = await _dispatcher.SendAsync(request);
                if (response.Success && response.Payload is CadInstanceInfo cad)
                {
                    SelectedCadInstance = cad;
                    StatusText = $"Selected CAD: {cad.Name}";
                }
                else
                    AppendReportLines(response.Diagnostics, "Pick CAD failed");
            }
            catch (Exception ex)
            {
                ReportLines.Add($"Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async void RefreshRevitDataAsync()
        {
            if (_dispatcher == null) return;
            IsBusy = true;
            ReportLines.Clear();
            try
            {
                var request = new RevitRequest
                {
                    Type = RevitRequestType.GetSelectableData,
                    CorrelationId = Guid.NewGuid(),
                    Payload = null
                };
                var response = await _dispatcher.SendAsync(request);
                if (response.Success && response.Payload is GetSelectableDataPayload data)
                {
                    Levels.Clear();
                    foreach (var l in data.Levels) Levels.Add(l);
                    WallTypes.Clear();
                    foreach (var w in data.WallTypes) WallTypes.Add(w);
                    CadInstances.Clear();
                    foreach (var c in data.CadInstances) CadInstances.Add(c);

                    if (Levels.Count > 0 && SelectedBaseLevel == null) SelectedBaseLevel = Levels[0];
                    if (Levels.Count > 1 && SelectedTopLevel == null) SelectedTopLevel = Levels[Levels.Count - 1];
                    foreach (var row in WallGroupRows)
                    {
                        if (row.SelectedWallType == null && WallTypes.Count > 0)
                            row.SelectedWallType = WallTypes[0];
                    }
                    StatusText = $"Levels: {Levels.Count}, Wall types: {WallTypes.Count}, CAD: {CadInstances.Count}";
                }
                else
                    AppendReportLines(response.Diagnostics, "Get selectable data failed");
            }
            catch (Exception ex)
            {
                ReportLines.Add($"Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanImportWalls()
        {
            if (string.IsNullOrEmpty(BimJsonPath) || SelectedBaseLevel == null || SelectedTopLevel == null ||
                SelectedCadInstance == null) return false;
            return WallGroupRows.All(r => r.SelectedWallType != null);
        }

        private async void ImportWallsAsync()
        {
            if (_dispatcher == null || !CanImportWalls()) return;
            IsBusy = true;
            ReportLines.Clear();
            try
            {
                var payload = new ImportWallsRequestPayload
                {
                    BimJsonPath = BimJsonPath,
                    CadElementIdString = SelectedCadInstance.ElementIdString,
                    BaseLevelElementIdString = SelectedBaseLevel.ElementIdString,
                    TopLevelElementIdString = SelectedTopLevel.ElementIdString,
                    WallTypeMappings = WallGroupRows
                        .Where(r => r.SelectedWallType != null)
                        .Select(r => new WallTypeMappingItem
                        {
                            ThicknessCm = r.ThicknessCm,
                            WallTypeElementIdString = r.SelectedWallType.ElementIdString
                        }).ToList()
                };
                var request = new RevitRequest
                {
                    Type = RevitRequestType.ImportWalls,
                    CorrelationId = Guid.NewGuid(),
                    Payload = payload
                };
                var response = await _dispatcher.SendAsync(request);
                if (response.Success && response.Payload is ImportReport report)
                {
                    ReportLines.Add($"Created: {report.Created}, Skipped: {report.Skipped}, Failed: {report.Failed}");
                    foreach (var item in report.ItemResults)
                        ReportLines.Add($"  {item.SourceWallId}: {item.Status} - {item.ReasonCode} {item.Message}");
                    StatusText = $"Import complete. Created {report.Created} walls.";
                }
                else
                    AppendReportLines(response.Diagnostics, "Import failed");
            }
            catch (Exception ex)
            {
                ReportLines.Add($"Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void AppendReportLines(System.Collections.Generic.List<Diagnostic> diagnostics, string header)
        {
            ReportLines.Add(header);
            if (diagnostics != null)
                foreach (var d in diagnostics)
                    ReportLines.Add($"  [{d.Severity}] {d.Code}: {d.Message}");
        }
    }
}
