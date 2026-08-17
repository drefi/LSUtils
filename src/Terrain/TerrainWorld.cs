namespace LSUtils.Terrain;

using System.Collections.Generic;
using System.Linq;
using LSUtils;
using LSUtils.Terrain.Navigation;
using LSUtils.Spatial;

public class TerrainWorld<TTerrainType, TContentType> {
    private readonly ISpatialIndex<TerrainPatch<TTerrainType>> _patchIndex;
    private readonly ISpatialIndex<TerrainContent<TContentType>> _contentIndex;
    private readonly HashSet<TerrainPatch<TTerrainType>> _patches = new();
    private readonly Dictionary<TerrainPatch<TTerrainType>, long> _patchVersions = new();
    private readonly HashSet<TerrainContent<TContentType>> _contents = new();
    private readonly Dictionary<TerrainContent<TContentType>, long> _contentVersions = new();
    private readonly Dictionary<TerrainContent<TContentType>, TerrainContentMobility> _contentMobility = new();
    private readonly HashSet<TerrainRegion<TTerrainType, TContentType>> _regions = new();

    public Bounds Bounds { get; }
    public TTerrainType DefaultTerrainType { get; }
    /// <summary>Changes whenever terrain or content geometry that may affect navigation changes.</summary>
    public long NavigationVersion { get; private set; }
    public long StaticNavigationVersion { get; private set; }
    public long DynamicNavigationVersion { get; private set; }
    public IReadOnlyCollection<TerrainPatch<TTerrainType>> Patches => _patches;
    public IReadOnlyCollection<TerrainContent<TContentType>> Contents => _contents;
    public IReadOnlyCollection<TerrainRegion<TTerrainType, TContentType>> Regions => _regions;

    public TerrainWorld(Bounds bounds, TTerrainType defaultTerrainType, float spatialCellSize = 64f) {
        Bounds = bounds;
        DefaultTerrainType = defaultTerrainType;
        _patchIndex = new SpatialHashGrid<TerrainPatch<TTerrainType>>(spatialCellSize);
        _contentIndex = new SpatialHashGrid<TerrainContent<TContentType>>(spatialCellSize);
    }

    public bool AddPatch(TerrainPatch<TTerrainType> patch) {
        if (!_patches.Add(patch)) return false;
        _patchIndex.Insert(patch);
        _patchVersions.Add(patch, patch.Version);
        patch.Changed += OnPatchChanged;
        NavigationVersion++;
        StaticNavigationVersion++;
        return true;
    }

    public bool RemovePatch(TerrainPatch<TTerrainType> patch) {
        if (!_patches.Remove(patch)) return false;
        patch.Changed -= OnPatchChanged;
        _patchIndex.Remove(patch);
        _patchVersions.Remove(patch);
        NavigationVersion++;
        StaticNavigationVersion++;
        return true;
    }

    public bool UpdatePatch(TerrainPatch<TTerrainType> patch) {
        if (!_patches.Contains(patch)) return false;
        if (_patchVersions[patch] == patch.Version) return true;
        if (!_patchIndex.Update(patch)) return false;
        _patchVersions[patch] = patch.Version;
        NavigationVersion++;
        StaticNavigationVersion++;
        return true;
    }

    public bool AddContent(TerrainContent<TContentType> content) {
        if (!_contents.Add(content)) return false;
        _contentIndex.Insert(content);
        _contentVersions.Add(content, content.Version);
        _contentMobility.Add(content, content.Mobility);
        content.Changed += OnContentChanged;
        NavigationVersion++;
        IncrementContentVersion(content.Mobility);
        return true;
    }

    public bool RemoveContent(TerrainContent<TContentType> content) {
        if (!_contents.Remove(content)) return false;
        content.Changed -= OnContentChanged;
        _contentIndex.Remove(content);
        _contentVersions.Remove(content);
        var mobility = _contentMobility[content];
        _contentMobility.Remove(content);
        NavigationVersion++;
        IncrementContentVersion(mobility);
        return true;
    }

    public bool UpdateContent(TerrainContent<TContentType> content) {
        if (!_contents.Contains(content)) return false;
        if (_contentVersions[content] == content.Version) return true;
        if (!_contentIndex.Update(content)) return false;
        var previousMobility = _contentMobility[content];
        _contentVersions[content] = content.Version;
        _contentMobility[content] = content.Mobility;
        NavigationVersion++;
        IncrementContentVersion(previousMobility);
        if (previousMobility != content.Mobility) IncrementContentVersion(content.Mobility);
        return true;
    }

    private void OnPatchChanged(TerrainPatch<TTerrainType> patch) {
        UpdatePatch(patch);
    }

    private void OnContentChanged(TerrainContent<TContentType> content) {
        UpdateContent(content);
    }

    private void IncrementContentVersion(TerrainContentMobility mobility) {
        if (mobility == TerrainContentMobility.Static) StaticNavigationVersion++;
        else DynamicNavigationVersion++;
    }

    public bool AddRegion(TerrainRegion<TTerrainType, TContentType> region) {
        return _regions.Add(region);
    }

    public bool RemoveRegion(TerrainRegion<TTerrainType, TContentType> region) {
        return _regions.Remove(region);
    }

    public List<TerrainPatch<TTerrainType>> QueryPatches(Bounds area) {
        return _patchIndex.Query(area);
    }

    public List<TerrainContent<TContentType>> QueryContents(Bounds area) {
        return _contentIndex.Query(area);
    }

    public List<TerrainPatch<TTerrainType>> QueryPatchesAt(float x, float y) {
        return QueryPatches(new Bounds(x, y, 0, 0))
            .Where(patch => patch.Contains(x, y))
            .ToList();
    }

    public TTerrainType ResolveTerrainTypeAt(float x, float y) {
        var patch = ResolvePatchAt(x, y);
        return patch == null ? DefaultTerrainType : patch.Type;
    }

    public TerrainPatch<TTerrainType>? ResolvePatchAt(float x, float y) {
        return QueryPatchesAt(x, y)
            .OrderByDescending(patch => patch.Layer)
            .ThenByDescending(patch => patch.Priority)
            .FirstOrDefault();
    }

    public List<LSVector2> FindPath(
        LSVector2 start,
        LSVector2 goal,
        TerrainNavigationSettings<TTerrainType, TContentType> settings) {
        return TerrainPathfinder.FindPath(this, start, goal, settings);
    }

    public TerrainNavigationMesh<TTerrainType, TContentType> BuildNavigationMesh(
        TerrainNavigationSettings<TTerrainType, TContentType> settings) {
        return BakeNavigationMesh(settings);
    }

    public TerrainNavigationMesh<TTerrainType, TContentType> BakeNavigationMesh(
        TerrainNavigationSettings<TTerrainType, TContentType> settings) {
        return TerrainPathfinder.BakeNavigationMesh(this, settings);
    }
}
