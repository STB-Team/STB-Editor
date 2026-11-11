using System.Text;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using StbEditor.App.Panels;
using StbEditor.App.Ui;
using StbEditor.Core;
using StbEditor.Data;

namespace StbEditor.App.Services;

/// <summary>Scratch-only VMAD regression; does not change the user's active file, settings or edit document.</summary>
internal sealed class AliasScriptsTest
{
    public bool Completed { get; private set; } = !Environment.GetCommandLineArgs().Contains("--autotest-aliasscripts");

    public void Step(EditorApp app)
    {
        if (Completed || app.Session is not { } session || app.Loader is not null) return;
        Completed = true;
        try
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "--autotest-aliasscripts");
            if (at + 1 >= args.Length || args[at + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("--autotest-aliasscripts requires a scratch directory");
            var directory = Path.GetFullPath(args[at + 1]);
            // An explicit scratch directory is required; never overwrite an installed asset/plugin directory.
            var forbidden = new List<string> { session.Game.GameFolder };
            if (session.Game.Mo2 is { } mo2) forbidden.Add(mo2.Folder);
            if (forbidden.Any(root => (directory + Path.DirectorySeparatorChar).StartsWith(
                Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("The scratch directory must be outside the game/mod folders");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "STBEditorAliasScriptsTest.esp");
            if (File.Exists(path)) throw new IOException($"Scratch output already exists: {path}");
            var active = ActivePlugin.ForNew(path, false, false, session.Game, session.Workspace.DefaultEncoding);
            var lookup = new RecordLookup(session.Workspace, session.Catalog);
            var wanted = at + 2 < args.Length && !args[at + 2].StartsWith("--", StringComparison.Ordinal) ? args[at + 2] : null;
            Quest initial;
            if (wanted is not null)
            {
                var key = session.Catalog.BySignature["QUST"].First(q => string.Equals(q.EditorId, wanted, StringComparison.OrdinalIgnoreCase)).FormKey;
                initial = lookup.Resolve<IQuestGetter>(key)!.DeepCopy();
            }
            else
            {
                initial = new Quest(active.Allocate(), session.Game.Release) { EditorID = "STBEditorAliasScriptsTestQuest", NextAliasID = 43 };
                initial.Aliases.Add(new QuestAlias { ID = 42, Name = "Player", Type = QuestAlias.TypeEnum.Reference });
                initial.Aliases.Add(new QuestAlias { ID = 7, Name = "Location", Type = QuestAlias.TypeEnum.Location });
                initial.VirtualMachineAdapter = new QuestAdapter { Version = 5, ObjectFormat = 2, ExtraBindDataVersion = 2, FileName = "STBAliasFragment" };
                initial.VirtualMachineAdapter.Scripts.Add(new ScriptEntry { Name = "STBQuestScript", Flags = ScriptEntry.Flag.Local });
                initial.VirtualMachineAdapter.Fragments.Add(new QuestScriptFragment { Stage = 10, StageIndex = 0, Unknown2 = 1, ScriptName = "STBAliasFragment", FragmentName = "Fragment_0" });
                AliasScripts.GetOrCreate(initial, 7).Scripts.Add(new ScriptEntry { Name = "STBLocationScript" });
            }
            var selected = initial.Aliases.FirstOrDefault(a => AliasScripts.Binding(initial, a.ID) is { Scripts.Count: > 0 }) ?? initial.Aliases.First();
            var id = selected.ID;
            var binding = AliasScripts.GetOrCreate(initial, id);
            var testScript = new ScriptEntry { Name = "STBEditorAliasScriptsRegression", Flags = ScriptEntry.Flag.Local };
            if (binding.Scripts.Any(s => s.Name == testScript.Name)) throw new InvalidOperationException("Regression script already exists");
            binding.Scripts.Add(testScript);
            testScript.Properties.Add(new ScriptIntProperty { Name = "Int", Data = 1, Flags = ScriptProperty.Flag.Edited });
            testScript.Properties.Add(new ScriptFloatProperty { Name = "Float", Data = 1.25f });
            testScript.Properties.Add(new ScriptBoolProperty { Name = "Bool", Data = false });
            testScript.Properties.Add(new ScriptStringProperty { Name = "String", Data = "before" });
            testScript.Properties.Add(new ScriptObjectProperty { Name = "Object", Alias = -1 });
            testScript.Properties.Add(new ScriptObjectProperty { Name = "Alias", Alias = -1 });
            testScript.Properties.Add(new ScriptIntListProperty { Name = "Ints" });
            testScript.Properties.Add(new ScriptFloatListProperty { Name = "Floats" });
            testScript.Properties.Add(new ScriptBoolListProperty { Name = "Bools" });
            testScript.Properties.Add(new ScriptStringListProperty { Name = "Strings" });
            testScript.Properties.Add(new ScriptObjectListProperty { Name = "Objects" });
            var externalQuest = session.Catalog.BySignature["QUST"].First(q => q.FormKey != initial.FormKey).FormKey;
            var external = new QuestFragmentAlias { Version = 5, ObjectFormat = 1, Property = new ScriptObjectProperty { Alias = unchecked((short)id) } };
            external.Property.Object.SetTo(externalQuest);
            external.Scripts.Add(new ScriptEntry { Name = "STBExternalBinding" });
            initial.VirtualMachineAdapter!.Aliases.Insert(0, external);
            var survivor = new ScriptEntry { Name = "STBEditorAliasReferenceRegression" };
            var scalar = new ScriptObjectProperty { Name = "Local", Alias = unchecked((short)id) };
            scalar.Object.SetTo(initial.FormKey);
            survivor.Properties.Add(scalar);
            var array = new ScriptObjectListProperty { Name = "Aliases" };
            var localItem = new ScriptObjectProperty { Alias = unchecked((short)id) };
            localItem.Object.SetTo(initial.FormKey);
            array.Objects.Add(localItem);
            var foreignItem = new ScriptObjectProperty { Alias = unchecked((short)id) };
            foreignItem.Object.SetTo(externalQuest);
            array.Objects.Add(foreignItem);
            survivor.Properties.Add(array);
            initial.VirtualMachineAdapter.Scripts.Add(survivor);
            var before = Snapshot(initial);
            var protectedBefore = Protected(initial, id);
            var document = new EditDocument();
            IQuestGetter Current() => (IQuestGetter?)OverrideEditing.Override(document, initial.FormKey)?.Record ?? initial;
            var target = new EditTarget(() => document.WorkingCopy(initial.FormKey, initial),
                (label, record) => document.CommitEdit(label, record, RecordPlace.TopLevel),
                record => RecordRoundTrip.Read(record, session.Game.Release, Encoding.UTF8) is null ? RecordRoundTrip.LastError ?? "Roundtrip failed" : null);
            target.Apply("Alias VMAD identity and properties", record =>
            {
                var quest = (Quest)record;
                // Changing list position must not redirect script edits to another alias or an external quest binding.
                var alias = quest.Aliases.First(a => a.ID == id);
                quest.Aliases.Remove(alias);
                quest.Aliases.Add(alias);
                var script = AliasScripts.GetOrCreate(quest, id).Scripts.First(s => s.Name == testScript.Name);
                ((ScriptIntProperty)script.Properties[0]).Data = 73;
                ((ScriptFloatProperty)script.Properties[1]).Data = 3.5f;
                ((ScriptBoolProperty)script.Properties[2]).Data = true;
                ((ScriptStringProperty)script.Properties[3]).Data = "after";
                ((ScriptObjectProperty)script.Properties[4]).Object.SetTo(new FormKey(Constants.Skyrim, 0x14));
                var aliasProperty = (ScriptObjectProperty)script.Properties[5];
                aliasProperty.Object.SetTo(quest.FormKey);
                aliasProperty.Alias = unchecked((short)id);
                ((ScriptIntListProperty)script.Properties[6]).Data.Add(12);
                ((ScriptFloatListProperty)script.Properties[7]).Data.Add(2.25f);
                ((ScriptBoolListProperty)script.Properties[8]).Data.Add(true);
                ((ScriptStringListProperty)script.Properties[9]).Data.Add("array");
                var item = new ScriptObjectProperty { Alias = unchecked((short)id) };
                item.Object.SetTo(quest.FormKey);
                ((ScriptObjectListProperty)script.Properties[10]).Objects.Add(item);
                var externalItem = new ScriptObjectProperty { Alias = unchecked((short)id) };
                externalItem.Object.SetTo(externalQuest);
                ((ScriptObjectListProperty)script.Properties[10]).Objects.Add(externalItem);
            });
            Require(document.PendingCount == 1, "alias script edit was not one pending quest");
            var edited = Snapshot(Current());
            Require(Protected(Current(), id) == protectedBefore, "quest scripts, fragments, versions/flags, other and external bindings preserved");
            VerifyValues(Current(), id, externalQuest);
            document.Undo();
            Require(Snapshot(Current()) == before, "undo");
            document.Redo();
            Require(Snapshot(Current()) == edited, "redo");
            target.Apply("Remove alias and local VMAD references", record => AliasScripts.Remove((Quest)record, id));
            Require(!Current().Aliases.Any(a => a.ID == id) && AliasScripts.Binding(Current(), id) is null, "removed binding");
            var removedVmad = Current().VirtualMachineAdapter ?? throw new InvalidOperationException("Alias removal dropped the quest VMAD");
            Require(removedVmad.Aliases.Any(a => a.Property.Object.FormKey == externalQuest && a.Property.Alias == unchecked((short)id)), "external same-ID binding preserved");
            var cleaned = removedVmad.Scripts.First(s => s.Name == survivor.Name);
            Require(!cleaned.Properties.Any(p => p.Name == "Local"), "local scalar reference cleaned");
            var cleanedArray = (IScriptObjectListPropertyGetter)cleaned.Properties.First(p => p.Name == "Aliases");
            Require(cleanedArray.Objects.Count == 2 && cleanedArray.Objects[0].Alias == -1 && cleanedArray.Objects[0].Object.FormKey.IsNull
                && cleanedArray.Objects[1].Object.FormKey == externalQuest && cleanedArray.Objects[1].Alias == unchecked((short)id),
                "array slots retained and only local references cleared");
            document.Undo();
            Require(Snapshot(Current()) == edited, "undo alias removal");
            var roundtrip = (IQuestGetter?)RecordRoundTrip.Read(Current(), session.Game.Release, Encoding.UTF8);
            Require(roundtrip is not null && Snapshot(roundtrip) == edited, "VMAD roundtrip");
            VerifyValues(roundtrip!, id, externalQuest);
            var result = new PluginWriter(session.Workspace, session.Catalog).Prepare(active, document);
            Require(result.Success, result.Error ?? "scratch save");
            File.Move(result.TempPath!, path);
            var reloaded = SkyrimMod.CreateFromBinary(new ModPath(active.ModKey, path), session.Game.Release);
            var saved = reloaded.Quests.First(q => q.FormKey == initial.FormKey);
            Require(Snapshot(saved) == edited, "scratch save/reload");
            VerifyValues(saved, id, externalQuest);
            Log.Info($"autotest-aliasscripts: PASS ({wanted ?? initial.EditorID}, alias ID {id}, all property types, identity, preservation, undo/redo, roundtrip, save/reload): {path}");
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            Log.Warn($"autotest-aliasscripts: FAIL: {ex}");
        }
    }

    private static void VerifyValues(IQuestGetter quest, uint id, FormKey externalQuest)
    {
        var properties = AliasScripts.Binding(quest, id)!.Scripts.First(s => s.Name == "STBEditorAliasScriptsRegression")
            .Properties.ToDictionary(p => p.Name);
        Require(((IScriptIntPropertyGetter)properties["Int"]).Data == 73
            && ((IScriptFloatPropertyGetter)properties["Float"]).Data == 3.5f
            && ((IScriptBoolPropertyGetter)properties["Bool"]).Data
            && ((IScriptStringPropertyGetter)properties["String"]).Data == "after", "scalar property values changed");
        var obj = (IScriptObjectPropertyGetter)properties["Object"];
        Require(obj.Object.FormKey == new FormKey(Constants.Skyrim, 0x14) && obj.Alias == -1, "object property lost reference identity");
        Require(AliasScripts.RefersTo((IScriptObjectPropertyGetter)properties["Alias"], quest.FormKey, id), "alias property lost quest or alias ID");
        Require(((IScriptIntListPropertyGetter)properties["Ints"]).Data.SequenceEqual([12])
            && ((IScriptFloatListPropertyGetter)properties["Floats"]).Data.SequenceEqual([2.25f])
            && ((IScriptBoolListPropertyGetter)properties["Bools"]).Data.SequenceEqual([true])
            && ((IScriptStringListPropertyGetter)properties["Strings"]).Data.SequenceEqual(["array"]), "scalar array values changed");
        var objects = ((IScriptObjectListPropertyGetter)properties["Objects"]).Objects;
        Require(objects.Count == 2 && AliasScripts.RefersTo(objects[0], quest.FormKey, id)
            && AliasScripts.RefersTo(objects[1], externalQuest, id), "object array lost local or external alias reference");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string Snapshot(IQuestGetter quest) => string.Join("\n", RecordFields.Leaves(quest.VirtualMachineAdapter!, 16, int.MaxValue)
        .Select(leaf => $"{leaf.PathText}={RecordFields.Repr(leaf.Value)}"));

    private static string Protected(IQuestGetter quest, uint id)
    {
        var copy = quest.DeepCopy();
        var binding = copy.VirtualMachineAdapter!.Aliases.First(a => AliasScripts.RefersTo(a.Property, copy.FormKey, id));
        binding.Scripts.RemoveAll(s => s.Name == "STBEditorAliasScriptsRegression");
        return Snapshot(copy);
    }
}
