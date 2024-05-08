using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using DynamicData;
using DynamicData.Binding;
using FortnitePorting.Controls;
using FortnitePorting.Controls.Assets;
using FortnitePorting.Models.Assets;
using FortnitePorting.Shared;
using FortnitePorting.Shared.Framework;
using FortnitePorting.Shared.Services;
using Material.Icons;
using ReactiveUI;

namespace FortnitePorting.ViewModels;

public partial class AssetsViewModel : ViewModelBase
{
    [ObservableProperty] private AssetLoaderCollection _assetLoaderCollection;

    [ObservableProperty] private ObservableCollection<AssetInfo> _selectedAssets = [];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SortIcon))]
    private bool _descendingSort = false;

    public MaterialIconKind SortIcon => DescendingSort ? MaterialIconKind.SortDescending : MaterialIconKind.SortAscending;
    public readonly IObservable<SortExpressionComparer<AssetItem>> AssetSort;
    
    public readonly IObservable<Func<AssetItem, bool>> AssetFilter;

    [ObservableProperty] private bool _isPaneOpen = true;
    [ObservableProperty] private EAssetSortType _sortType = EAssetSortType.None;
    [ObservableProperty] private EExportLocation _exportLocation = EExportLocation.Blender;

    public AssetsViewModel()
    {
        AssetFilter = this
            .WhenAnyValue(x => x.AssetLoaderCollection.ActiveLoader.SearchFilter)
            .Select(CreateAssetFilter);
        
        AssetSort = this
            .WhenAnyValue(viewModel => viewModel.SortType, viewModel => viewModel.DescendingSort)
            .Select(CreateAssetSort);
    }

    public override async Task Initialize()
    {
        AssetLoaderCollection = new AssetLoaderCollection();
        await AssetLoaderCollection.Load(EAssetType.Outfit);
    }
    
    private static Func<AssetItem, bool> CreateAssetFilter(string searchFilter)
    {
        return asset => asset.Match(searchFilter);
    }

    private static SortExpressionComparer<AssetItem> CreateAssetSort((EAssetSortType, bool) values)
    {
        var (type, descending) = values;
        Func<AssetItem, IComparable> sort = type switch
        {
            EAssetSortType.AZ => asset => asset.CreationData.DisplayName,
            EAssetSortType.Season => asset => asset.Season + (double) asset.Rarity * 0.01,
            EAssetSortType.Rarity => asset => asset.Series?.DisplayName.Text + (int) asset.Rarity,
            _ => asset => asset.CreationData.Object.Name
        };

        return descending
            ? SortExpressionComparer<AssetItem>.Descending(sort)
            : SortExpressionComparer<AssetItem>.Ascending(sort);
    }
}