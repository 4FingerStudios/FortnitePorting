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
using FortnitePorting.Models.Assets;
using FortnitePorting.Shared;
using FortnitePorting.Shared.Framework;
using FortnitePorting.Shared.Services;
using Material.Icons;
using ReactiveUI;

namespace FortnitePorting.ViewModels;

public partial class AssetsViewModel : ViewModelBase
{
    public List<AssetLoader> Loaders;

    [ObservableProperty] private AssetLoader _currentAssetLoader;
    [ObservableProperty] private ReadOnlyObservableCollection<AssetItem> _currentAssetCollection;
    
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SortIcon))] private bool _descendingSort = false;
    public MaterialIconKind SortIcon => DescendingSort ? MaterialIconKind.SortDescending : MaterialIconKind.SortAscending;
    public readonly IObservable<SortExpressionComparer<AssetItem>> AssetSort;
    
    [ObservableProperty] private EAssetSortType _sortType = EAssetSortType.None;
    [ObservableProperty] private EExportLocation _exportLocation = EExportLocation.Blender;

    public AssetsViewModel()
    {
        AssetSort = this
            .WhenAnyValue(viewModel => viewModel.SortType, viewModel => viewModel.DescendingSort)
            .Select(CreateAssetSort);
    }
    
    private static SortExpressionComparer<AssetItem> CreateAssetSort((EAssetSortType, bool) values)
    {
        var (type, descending) = values;

        //AssetsVM.ModifyFilters("Battle Royale", type is ESortType.Season);
        
        Func<AssetItem, IComparable> sort = type switch
        {
            EAssetSortType.None => asset => asset.CreationData.Object.Name,
            EAssetSortType.AZ => asset => asset.CreationData.DisplayName,
            // scuffed ways to do sub-sorting within sections
            EAssetSortType.Season => asset => asset.Season + (double) asset.Rarity * 0.01,
            EAssetSortType.Rarity => asset => asset.Series?.DisplayName.Text + (int) asset.Rarity,
            _ => asset => asset.CreationData.Object.Name
        };

        return descending
            ? SortExpressionComparer<AssetItem>.Descending(sort)
            : SortExpressionComparer<AssetItem>.Ascending(sort);
    }

    public override async Task Initialize()
    {
        Loaders = 
        [
            new AssetLoader(EAssetType.Outfit)
            {
                ClassNames = ["AthenaCharacterItemDefinition"],
                PlaceholderIconPath = "FortniteGame/Content/Athena/Prototype/Textures/T_Placeholder_Item_Outfit",
                IconHandler = asset =>
                {
                    asset.TryGetValue(out UTexture2D? previewImage, "SmallPreviewImage", "LargePreviewImage");
                    if (asset.TryGetValue(out UObject hero, "HeroDefinition")) 
                        hero.TryGetValue(out previewImage, "SmallPreviewImage", "LargePreviewImage");

                    return previewImage;
                }
            }
        ];
        SetAssetType(EAssetType.Outfit);
        await CurrentAssetLoader.Load();
    }

    public void SetAssetType(EAssetType assetType)
    {
        CurrentAssetLoader = Loaders.First(loader => loader.Type == assetType);
        CurrentAssetCollection = CurrentAssetLoader.Filtered;
    }

    public void ResetDebug()
    {
        CurrentAssetLoader = new AssetLoader(EAssetType.Outfit)
        {
            ClassNames = ["AthenaCharacterItemDefinition"],
            PlaceholderIconPath = "FortniteGame/Content/Athena/Prototype/Textures/T_Placeholder_Item_Outfit",
            IconHandler = asset =>
            {
                asset.TryGetValue(out UTexture2D? previewImage, "SmallPreviewImage", "LargePreviewImage");
                if (asset.TryGetValue(out UObject hero, "HeroDefinition"))
                    hero.TryGetValue(out previewImage, "SmallPreviewImage", "LargePreviewImage");

                return previewImage;
            }
        };
        CurrentAssetCollection = CurrentAssetLoader.Filtered;
        CurrentAssetLoader.Source.Clear();
        TaskService.Run(CurrentAssetLoader.Load);
    }
}