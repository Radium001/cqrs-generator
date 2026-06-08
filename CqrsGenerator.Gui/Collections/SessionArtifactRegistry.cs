using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Collections;

public sealed class SessionArtifactRegistry : ObservableObject
{
    public Dictionary<string, NewEntityDraft> EntityDrafts { get; } = new();
    public Dictionary<string, NewDtoDraft> DtoDrafts { get; } = new();
    public Dictionary<string, AddQueryFormState> QueryDrafts { get; } = new();

    public ObservableCollection<EntityInfo> EntityInfos { get; } = new();
    public ObservableCollection<DtoInfo> DtoInfos { get; } = new();
    public ObservableCollection<QueryInfo> QueryInfos { get; } = new();

    public bool HasAnyDraft =>
        EntityDrafts.Count > 0 || DtoDrafts.Count > 0 || QueryDrafts.Count > 0;

    public int EntityCount => EntityInfos.Count;
    public int DtoCount => DtoInfos.Count;
    public int QueryCount => QueryInfos.Count;

    public void PublishEntity(string key, NewEntityDraft draft, EntityInfo info)
    {
        EntityDrafts[key] = draft;
        if (!EntityInfos.Any(e => e.Name == info.Name))
            EntityInfos.Add(info);
    }

    public void PublishDto(string key, NewDtoDraft draft, DtoInfo info)
    {
        DtoDrafts[key] = draft;
        if (!DtoInfos.Any(d => d.Name == info.Name))
            DtoInfos.Add(info);
    }

    public void PublishQuery(string key, AddQueryFormState draft, QueryInfo info)
    {
        QueryDrafts[key] = draft;
        if (!QueryInfos.Any(q => q.Name == info.Name))
            QueryInfos.Add(info);
    }

    public bool RemoveEntity(string key)
    {
        var removed = EntityDrafts.Remove(key);
        var info = EntityInfos.FirstOrDefault(e => e.Name == key);
        if (info is not null)
            EntityInfos.Remove(info);
        return removed;
    }

    public bool RemoveDto(string key)
    {
        var removed = DtoDrafts.Remove(key);
        var info = DtoInfos.FirstOrDefault(d => d.Name == key);
        if (info is not null)
            DtoInfos.Remove(info);
        return removed;
    }

    public bool RemoveQuery(string draftKey)
    {
        var removed = QueryDrafts.Remove(draftKey);
        var info = QueryInfos.FirstOrDefault(q => q.Name == draftKey);
        if (info is not null)
            QueryInfos.Remove(info);
        return removed;
    }

    public void ClearFeatureArtifacts()
    {
        EntityDrafts.Clear();
        DtoDrafts.Clear();
        QueryDrafts.Clear();
        EntityInfos.Clear();
        DtoInfos.Clear();
        QueryInfos.Clear();
    }
}
