using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace StbEditor.Core;

/// <summary>Quest VMAD alias bindings use a quest FormKey and alias ID, never an alias-list offset.</summary>
public static class AliasScripts
{
    public static bool RefersTo(IScriptObjectPropertyGetter property, FormKey quest, uint aliasId) =>
        property.Object.FormKey == quest && property.Alias != -1 && unchecked((ushort)property.Alias) == aliasId;

    public static IQuestFragmentAliasGetter? Binding(IQuestGetter quest, uint aliasId) =>
        quest.VirtualMachineAdapter?.Aliases.FirstOrDefault(a => RefersTo(a.Property, quest.FormKey, aliasId));

    public static QuestFragmentAlias GetOrCreate(Quest quest, uint aliasId)
    {
        if (!quest.Aliases.Any(a => a.ID == aliasId)) throw new InvalidOperationException("The quest alias no longer exists.");
        if (aliasId >= ushort.MaxValue) throw new InvalidOperationException("The alias ID cannot be represented in VMAD.");
        var vmad = quest.VirtualMachineAdapter ??= new QuestAdapter { Version = 5, ObjectFormat = 2, ExtraBindDataVersion = 2 };
        var binding = vmad.Aliases.FirstOrDefault(a => RefersTo(a.Property, quest.FormKey, aliasId));
        if (binding is not null) return binding;
        binding = new QuestFragmentAlias
        {
            Version = vmad.Version,
            ObjectFormat = vmad.ObjectFormat,
            Property = new ScriptObjectProperty { Alias = unchecked((short)aliasId) },
        };
        binding.Property.Object.SetTo(quest.FormKey);
        vmad.Aliases.Add(binding);
        RecordFields.Unbreak(quest, ["VirtualMachineAdapter", "Aliases"]);
        return binding;
    }

    /// <summary>Drop only this quest's binding and script references to the removed local alias. External quests are untouched.</summary>
    public static void Remove(Quest quest, uint aliasId)
    {
        var alias = quest.Aliases.FirstOrDefault(a => a.ID == aliasId);
        if (alias is null) return;
        quest.Aliases.Remove(alias);
        if (quest.VirtualMachineAdapter is not { } vmad) return;
        vmad.Aliases.RemoveAll(a => RefersTo(a.Property, quest.FormKey, aliasId));
        foreach (var script in vmad.Scripts.Concat(vmad.Aliases.SelectMany(a => a.Scripts)))
        {
            script.Properties.RemoveAll(p => p is ScriptObjectProperty o && RefersTo(o, quest.FormKey, aliasId));
            foreach (var array in script.Properties.OfType<ScriptObjectListProperty>())
                foreach (var item in array.Objects.Where(o => RefersTo(o, quest.FormKey, aliasId)))
                {
                    item.Object.Clear();
                    item.Alias = -1;
                }
        }
    }
}
