using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AutoPowerMeter.ViewModels;

public sealed class TestCaseGroupViewModel : ObservableObject
{
    private bool _updating;
    public string Name { get; }
    public ObservableCollection<TestCaseViewModel> Cases { get; }
    public string Summary => $"{Name}（{Cases.Count} 条）";

    public TestCaseGroupViewModel(string name, IEnumerable<TestCaseViewModel> cases)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "未分组" : name;
        Cases = new ObservableCollection<TestCaseViewModel>(cases);
        foreach (var item in Cases) item.PropertyChanged += OnCasePropertyChanged;
    }

    public bool IsSelected
    {
        get => Cases.Count > 0 && Cases.All(x => x.IsSelected);
        set
        {
            _updating = true;
            foreach (var item in Cases) item.IsSelected = value;
            _updating = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedCount));
        }
    }

    public int SelectedCount => Cases.Count(x => x.IsSelected);

    private void OnCasePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updating || e.PropertyName != nameof(TestCaseViewModel.IsSelected)) return;
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(SelectedCount));
    }
}
