using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;

using FortnitePorting.Shared.Extensions;
using SharpGLTF.Schema2;

namespace FortnitePorting.Models.Assets;

public partial class AssetInfo : ObservableObject
{
    [ObservableProperty] private AssetItem _asset;
    [ObservableProperty] private ObservableCollection<AssetStyleInfo> _styleInfos = [];
    
    public AssetInfo(AssetItem asset)
    {
        Asset = asset;
        if (Asset.CreationData.Object is null) return;
        
        var styles = Asset.CreationData.Object.GetOrDefault("ItemVariants", Array.Empty<UObject>());
        foreach (var style in styles)
        {
            var channel = style.GetOrDefault("VariantChannelName", new FText("Style")).Text.ToLower().TitleCase();
            var optionsName = style.ExportType switch
            {
                "FortCosmeticCharacterPartVariant" => "PartOptions",
                "FortCosmeticMaterialVariant" => "MaterialOptions",
                "FortCosmeticParticleVariant" => "ParticleOptions",
                "FortCosmeticMeshVariant" => "MeshOptions",
                "FortCosmeticGameplayTagVariant" => "GenericTagOptions",
                _ => null
            };

            if (optionsName is null) continue;

            var options = style.Get<FStructFallback[]>(optionsName);
            if (options.Length == 0) continue;

            var styleInfo = new AssetStyleInfo(channel, options, Asset.IconDisplayImage);
            if (styleInfo.StyleDatas.Count > 0) StyleInfos.Add(styleInfo);
        }
    }
    
    public AssetInfo(AssetItem asset, IEnumerable<string> stylePaths)
    {
        Asset = asset;
        if (Asset.CreationData.Object is null) return;

        var styleObjects = new List<UObject>();
        foreach (var stylePath in stylePaths)
        {
            if (CUE4ParseVM.Provider.TryLoadObject(stylePath, out var styleObject))
            {
                styleObjects.Add(styleObject);
            }
        }
        
        var styleInfo = new AssetStyleInfo("Styles", styleObjects, Asset.IconDisplayImage);
        if (styleInfo.StyleDatas.Count > 0) StyleInfos.Add(styleInfo);
    }
    
    public BaseStyleData[] GetSelectedStyles()
    {
        return StyleInfos
            .SelectMany(info => info.SelectedItems)
            .ToArray();
    }
}