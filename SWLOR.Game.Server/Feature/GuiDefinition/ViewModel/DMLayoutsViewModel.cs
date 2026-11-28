using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SWLOR.Game.Server.Entity;
using SWLOR.Game.Server.Service;
using SWLOR.Game.Server.Service.DBService;
using SWLOR.Game.Server.Service.DMToolService;
using SWLOR.Game.Server.Service.GuiService;
using SWLOR.Game.Server.Service.GuiService.Component;
using SWLOR.Game.Server.Service.LogService;
using SWLOR.NWN.API.NWNX;
using SWLOR.NWN.API.NWScript.Enum;
using SWLOR.NWN.API.NWScript.Enum.VisualEffect;

namespace SWLOR.Game.Server.Feature.GuiDefinition.ViewModel
{
    public class DMLayoutsViewModel: GuiViewModelBase<DMLayoutsViewModel, GuiPayloadBase>
    {
        private const int EntriesPerPage = 20;
        private const int MaxLayoutsPerArea = 30;
        private const int MaxSpawnAttemptsPerEntry = 200;
        private const int LayoutLoadCooldownSeconds = 5;
        private const string NextAreaLayoutLoadTimestampVariable = "DMTOOLS_NEXT_AREA_LAYOUT_LOAD_TS";
        private const int LayoutSaveLockTimeoutSeconds = 10;
        private const string AreaLayoutSaveLockTimestampVariable = "DMTOOLS_AREA_LAYOUT_SAVE_LOCK_TS";
        private const string EditingDungeonMasterIdVariable = "DMTOOLS_EDITING_DM_ID";
        private const string EditingDungeonMasterNameVariable = "DMTOOLS_EDITING_DM_NAME";
        private const string EditingLastTouchTimestampVariable = "DMTOOLS_EDITING_LAST_TS";
        private const int EditingClaimStaleSeconds = 120;
        private bool _skipPaginationSearch;
        private int SelectedEntryIndex { get; set; }
        private readonly List<uint> _allEntries = new();
        private readonly List<string> _allEntryDisplayNames = new();
        private readonly List<uint> _entries = new();
        private readonly List<string> _entryDisplayNames = new();
        private readonly List<string> _layoutIds = new();
        private int _selectedLayoutIndex;
        private uint _highlightedEntry;

        public string Instructions
        {
            get => Get<string>();
            set => Set(value);
        }

        public GuiColor InstructionColor
        {
            get => Get<GuiColor>();
            set => Set(value);
        }

        public int SelectedType
        {
            get => Get<int>();
            set 
            { 
                Set(value);
                RebuildEntries();
                Search();
            }
        }

        public GuiBindingList<string> EntryNames
        {
            get => Get<GuiBindingList<string>>();
            set => Set(value);
        }

        public GuiBindingList<bool> PlaceableToggles
        {
            get => Get<GuiBindingList<bool>>();
            set => Set(value);
        }

        public GuiBindingList<GuiComboEntry> PageNumbers
        {
            get => Get<GuiBindingList<GuiComboEntry>>();
            set => Set(value);
        }

        public int SelectedPageIndex
        {
            get => Get<int>();
            set
            {
                Set(value);

                if (!_skipPaginationSearch)
                    Search();
            }
        }

        public string SearchText
        {
            get => Get<string>();
            set => Set(value);
        }

        public bool IsPlaceableSelected
        {
            get => Get<bool>();
            set => Set(value);
        }

        public string LayoutName
        {
            get => Get<string>();
            set => Set(value);
        }

        public bool IsLayoutSelected
        {
            get => Get<bool>();
            set => Set(value);
        }

        public GuiBindingList<string> LayoutNames
        {
            get => Get<GuiBindingList<string>>();
            set => Set(value);
        }

        public GuiBindingList<bool> LayoutToggles
        {
            get => Get<GuiBindingList<bool>>();
            set => Set(value);
        }

        protected override void Initialize(GuiPayloadBase initialPayload)
        {
            _skipPaginationSearch = true;
            Instructions = string.Empty;
            InstructionColor = GuiColor.Green;
            SelectedType = (int)ObjectType.Placeable;
            _highlightedEntry = OBJECT_INVALID;
            SelectedEntryIndex = -1;
            SelectedPageIndex = 0;
            IsPlaceableSelected = false;
            SearchText = string.Empty;
            LayoutName = string.Empty;
            IsLayoutSelected = false;
            EntryNames = new GuiBindingList<string>();
            PlaceableToggles = new GuiBindingList<bool>();
            PageNumbers = new GuiBindingList<GuiComboEntry>();
            LayoutNames = new GuiBindingList<string>();
            LayoutToggles = new GuiBindingList<bool>();

            RebuildEntries();
            Search();
            LoadLayouts();

            WatchOnClient(model => model.SelectedType);
            WatchOnClient(model => model.SelectedPageIndex);
            WatchOnClient(model => model.SearchText);
            WatchOnClient(model => model.LayoutName);
            _skipPaginationSearch = false;
        }

        private string GetAreaResref()
        {
            return GetResRef(GetArea(Player));
        }

        private void LoadLayouts()
        {
            var query = new DBQuery<DMAreaPlaceableLayout>()
                .AddFieldSearch(nameof(DMAreaPlaceableLayout.AreaResref), GetAreaResref(), false)
                .OrderBy(nameof(DMAreaPlaceableLayout.Name));
            var layouts = DB.Search(query);

            var layoutNames = new GuiBindingList<string>();
            var layoutToggles = new GuiBindingList<bool>();
            _layoutIds.Clear();
            _selectedLayoutIndex = -1;
            IsLayoutSelected = false;

            foreach (var layout in layouts)
            {
                _layoutIds.Add(layout.Id);
                layoutNames.Add(layout.Name);
                layoutToggles.Add(false);
            }

            LayoutNames = layoutNames;
            LayoutToggles = layoutToggles;
        }

        private void UpdatePagination(int totalRecordCount)
        {
            _skipPaginationSearch = true;
            var pagination = GuiPaginationState.Create(
                totalRecordCount,
                EntriesPerPage,
                SelectedPageIndex);
            PageNumbers = pagination.PageNumbers;
            SelectedPageIndex = pagination.SelectedPageIndex;

            _skipPaginationSearch = false;
        }

        private void RebuildEntries()
        {
            _allEntries.Clear();
            _allEntryDisplayNames.Clear();

            var area = GetArea(Player);
            for (var obj = GetFirstObjectInArea(area); GetIsObjectValid(obj); obj = GetNextObjectInArea(area))
            {

                if (GetObjectType(obj) != (ObjectType)SelectedType)
                    continue;

                if (!DMActions.IsDMSpawnedObject(obj))
                    continue;

                var objectName = GetName(obj);
                if (string.IsNullOrWhiteSpace(objectName))
                    objectName = GetResRef(obj);

                _allEntries.Add(obj);
                _allEntryDisplayNames.Add($"{objectName} [{GetTag(obj)}]");
            }
        }

        private void Search()
        {
            var placeableNames = new GuiBindingList<string>();
            var placeableToggles = new GuiBindingList<bool>();

            ClearHighlight();
            _entries.Clear();
            _entryDisplayNames.Clear();
            SelectedEntryIndex = -1;
            IsPlaceableSelected = false;

            var filteredIndexes = Enumerable.Range(0, _allEntries.Count);
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchUpper = GetStringUpperCase(SearchText);
                filteredIndexes = filteredIndexes.Where(x =>
                    GetStringUpperCase(_allEntryDisplayNames[x]).Contains(searchUpper));
            }

            var indexList = filteredIndexes.ToList();
            UpdatePagination(indexList.Count);
            var pageOffset = SelectedPageIndex * EntriesPerPage;
            var pageIndexes = indexList.Skip(pageOffset).Take(EntriesPerPage);

            foreach (var index in pageIndexes)
            {
                _entries.Add(_allEntries[index]);
                _entryDisplayNames.Add(_allEntryDisplayNames[index]);
                placeableNames.Add(_allEntryDisplayNames[index]);
                placeableToggles.Add(false);
            }

            EntryNames = placeableNames;
            PlaceableToggles = placeableToggles;

            if (_allEntries.Count <= 0)
            {
                Instructions = "No DM-spawned placeables found in this area.";
                InstructionColor = GuiColor.Red;
            }
            else if (_entries.Count <= 0)
            {
                Instructions = "No placeables match your search.";
                InstructionColor = GuiColor.Red;
            }
            else
            {
                Instructions = "Select a placeable to begin editing.";
                InstructionColor = GuiColor.Green;
            }
        }

        private void ClearHighlight()
        {
            if (!GetIsObjectValid(_highlightedEntry))
                return;

            ReleaseEditingClaimIfOwned(_highlightedEntry);
            PlayerPlugin.ApplyLoopingVisualEffectToObject(Player, _highlightedEntry, VisualEffect.None);
            _highlightedEntry = OBJECT_INVALID;
        }

        private (uint dm, string dmId, string dmName) GetActiveDungeonMaster()
        {
            var dm = Player;
            if (GetIsDMPossessed(dm))
            {
                var master = GetMaster(dm);
                if (GetIsObjectValid(master))
                    dm = master;
            }

            return (dm, GetObjectUUID(dm), GetName(dm));
        }

        private static int NowUnix()
        {
            return (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private bool IsEditingClaimStale(uint placeable)
        {
            var lastTouch = GetLocalInt(placeable, EditingLastTouchTimestampVariable);
            return lastTouch > 0 && NowUnix() - lastTouch > EditingClaimStaleSeconds;
        }

        private void TouchClaim(uint placeable, string dmId, string dmName)
        {
            SetLocalString(placeable, EditingDungeonMasterIdVariable, dmId);
            SetLocalString(placeable, EditingDungeonMasterNameVariable, dmName);
            SetLocalInt(placeable, EditingLastTouchTimestampVariable, NowUnix());
        }

        private void ReleaseEditingClaimIfOwned(uint placeable)
        {
            if (!GetIsObjectValid(placeable))
                return;

            var (_, dmId, _) = GetActiveDungeonMaster();
            var currentId = GetLocalString(placeable, EditingDungeonMasterIdVariable);
            if (currentId != dmId)
                return;

            DeleteLocalString(placeable, EditingDungeonMasterIdVariable);
            DeleteLocalString(placeable, EditingDungeonMasterNameVariable);
            DeleteLocalInt(placeable, EditingLastTouchTimestampVariable);
        }

        private bool TryClaimPlaceable(uint placeable, out string ownerName)
        {
            ownerName = string.Empty;

            var (_, dmId, dmName) = GetActiveDungeonMaster();
            var currentId = GetLocalString(placeable, EditingDungeonMasterIdVariable);
            if (string.IsNullOrWhiteSpace(currentId) || currentId == dmId || IsEditingClaimStale(placeable))
            {
                TouchClaim(placeable, dmId, dmName);
                return true;
            }

            ownerName = GetLocalString(placeable, EditingDungeonMasterNameVariable);
            return false;
        }

        private bool EnsureEditingOwnership(uint placeable)
        {
            var (_, dmId, dmName) = GetActiveDungeonMaster();
            var currentId = GetLocalString(placeable, EditingDungeonMasterIdVariable);
            if (currentId == dmId || IsEditingClaimStale(placeable))
            {
                TouchClaim(placeable, dmId, dmName);
                return true;
            }

            var ownerName = GetLocalString(placeable, EditingDungeonMasterNameVariable);
            if (string.IsNullOrWhiteSpace(ownerName))
                ownerName = "another DM";

            Instructions = $"That placeable is currently being edited by {ownerName}.";
            InstructionColor = GuiColor.Red;
            return false;
        }

        private void ApplyHighlight(uint placeable)
        {
            ClearHighlight();
            PlayerPlugin.ApplyLoopingVisualEffectToObject(Player, placeable, VisualEffect.Vfx_Dur_Aura_Green);
            _highlightedEntry = placeable;
        }

        private uint GetSelectedPlaceable()
        {
            if (SelectedEntryIndex < 0 || SelectedEntryIndex >= _entries.Count)
                return OBJECT_INVALID;

            var placeable = _entries[SelectedEntryIndex];
            if (!GetIsObjectValid(placeable))
            {
                Instructions = "Selected placeable no longer exists (possibly removed by another DM). List refreshed.";
                InstructionColor = GuiColor.Red;
                ClearHighlight();
                RebuildEntries();
                Search();
                return OBJECT_INVALID;
            }

            return placeable;
        }

        public Action OnCloseWindow() => () =>
        {
            ClearHighlight();
        };

        public Action OnOpenBattleGrid() => () =>
        {
            DMPresence.ToggleWindow(Player, GuiWindowType.DMBattleGrid);
        };

        public Action OnRefreshPlaceables() => () =>
        {
            RebuildEntries();
            Search();
        };

        public Action OnPreviousPage() => () =>
        {
            _skipPaginationSearch = true;
            var newPage = SelectedPageIndex - 1;
            if (newPage < 0)
                newPage = 0;

            SelectedPageIndex = newPage;
            Search();
            _skipPaginationSearch = false;
        };

        public Action OnNextPage() => () =>
        {
            _skipPaginationSearch = true;
            var newPage = SelectedPageIndex + 1;
            if (newPage > PageNumbers.Count - 1)
                newPage = PageNumbers.Count - 1;

            SelectedPageIndex = newPage;
            Search();
            _skipPaginationSearch = false;
        };

        public Action OnSelectPlaceable() => () =>
        {
            var previousIndex = SelectedEntryIndex;
            if (previousIndex > -1 && previousIndex < _entries.Count)
                ReleaseEditingClaimIfOwned(_entries[previousIndex]);

            if (SelectedEntryIndex > -1 && SelectedEntryIndex < PlaceableToggles.Count)
                PlaceableToggles[SelectedEntryIndex] = false;

            SelectedEntryIndex = NuiGetEventArrayIndex();
            if (SelectedEntryIndex < 0 || SelectedEntryIndex >= PlaceableToggles.Count)
                return;

            PlaceableToggles[SelectedEntryIndex] = true;
            IsPlaceableSelected = true;
            var placeable = GetSelectedPlaceable();
            if (!GetIsObjectValid(placeable))
            {
                IsPlaceableSelected = false;
                return;
            }

            if (!TryClaimPlaceable(placeable, out var ownerName))
            {
                PlaceableToggles[SelectedEntryIndex] = false;
                SelectedEntryIndex = -1;
                IsPlaceableSelected = false;
                Instructions = $"That placeable is currently being edited by {ownerName}.";
                InstructionColor = GuiColor.Red;
                ClearHighlight();
                return;
            }

            ApplyHighlight(placeable);
            Instructions = $"Editing: {_entryDisplayNames[SelectedEntryIndex]}";
            InstructionColor = GuiColor.Green;
        };

        public Action OnSearch() => () =>
        {
            SelectedPageIndex = 0;
            Search();
        };

        public Action OnClearSearch() => () =>
        {
            SearchText = string.Empty;
            SelectedPageIndex = 0;
            Search();
        };

        public Action OnSelectLayout() => () =>
        {
            if (_selectedLayoutIndex > -1 && _selectedLayoutIndex < LayoutToggles.Count)
                LayoutToggles[_selectedLayoutIndex] = false;

            _selectedLayoutIndex = NuiGetEventArrayIndex();
            if (_selectedLayoutIndex < 0 || _selectedLayoutIndex >= LayoutToggles.Count)
                return;

            LayoutToggles[_selectedLayoutIndex] = true;
            IsLayoutSelected = true;
            LayoutName = LayoutNames[_selectedLayoutIndex];
        };

        private List<DMAreaPlaceableLayoutEntry> BuildCurrentLayoutEntries()
        {
            RebuildEntries();
            var entries = new List<DMAreaPlaceableLayoutEntry>();
            var groupedCounts = new Dictionary<string, int>();

            foreach (var placeable in _allEntries)
            {
                var tag = GetTag(placeable);
                var resref = GetResRef(placeable);
                var key = $"{tag}|{resref}";
                if (!groupedCounts.ContainsKey(key))
                    groupedCounts[key] = 0;

                var position = GetPosition(placeable);
                var facing = GetFacing(placeable);
                entries.Add(new DMAreaPlaceableLayoutEntry
                {
                    Tag = tag,
                    Resref = resref,
                    MatchIndex = groupedCounts[key],
                    X = position.X,
                    Y = position.Y,
                    Z = position.Z,
                    Facing = facing
                });
                groupedCounts[key]++;
            }

            return entries;
        }

        public Action OnSaveLayout() => () =>
        {
            var layoutName = (LayoutName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(layoutName))
            {
                Instructions = "Enter a layout name before saving.";
                InstructionColor = GuiColor.Red;
                return;
            }

            var entries = BuildCurrentLayoutEntries();
            if (entries.Count <= 0)
            {
                Instructions = "No DM-spawned placeables available to save.";
                InstructionColor = GuiColor.Red;
                return;
            }

            var area = GetArea(Player);
            if (!GetIsObjectValid(area))
            {
                Instructions = "Area is invalid.";
                InstructionColor = GuiColor.Red;
                return;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var lockUntil = GetLocalInt(area, AreaLayoutSaveLockTimestampVariable);
            if (lockUntil > now)
            {
                Instructions = "Another DM is currently saving layouts in this area. Please try again in a moment.";
                InstructionColor = GuiColor.Red;
                return;
            }

            SetLocalInt(area, AreaLayoutSaveLockTimestampVariable, (int)now + LayoutSaveLockTimeoutSeconds);
            try
            {
                var areaResref = GetResRef(area);
                var areaQuery = new DBQuery<DMAreaPlaceableLayout>()
                    .AddFieldSearch(nameof(DMAreaPlaceableLayout.AreaResref), areaResref, false);
                var areaLayouts = DB.Search(areaQuery).ToList();
                var existing = areaLayouts
                    .FirstOrDefault(x => string.Equals(x.Name, layoutName, StringComparison.OrdinalIgnoreCase));

                if (existing == null && areaLayouts.Count >= MaxLayoutsPerArea)
                {
                    Instructions = $"This area already has {MaxLayoutsPerArea} layouts. Delete one before saving a new layout.";
                    InstructionColor = GuiColor.Red;
                    return;
                }

                if (existing == null)
                {
                    existing = new DMAreaPlaceableLayout
                    {
                        AreaResref = areaResref,
                        Name = layoutName
                    };
                }

                LayoutName = layoutName;
                existing.Entries = entries;
                DB.Set(existing);
                LoadLayouts();
                Instructions = $"Layout '{layoutName}' saved ({entries.Count} placeables).";
                InstructionColor = GuiColor.Green;
            }
            catch (Exception ex)
            {
                Log.Write(LogGroup.Error, $"Failed to save DM placeable layout '{layoutName}' in area '{GetResRef(area)}'. {ex}");
                Instructions = "Unable to save layout right now. Please try again.";
                InstructionColor = GuiColor.Red;
            }
            finally
            {
                SetLocalInt(area, AreaLayoutSaveLockTimestampVariable, 0);
            }
        };

        public Action OnLoadLayout() => () =>
        {
            if (!IsLayoutSelected || _selectedLayoutIndex < 0 || _selectedLayoutIndex >= _layoutIds.Count)
            {
                Instructions = "Select a layout to load.";
                InstructionColor = GuiColor.Red;
                return;
            }

            var area = GetArea(Player);
            if (!GetIsObjectValid(area))
            {
                Instructions = "Area is invalid.";
                InstructionColor = GuiColor.Red;
                return;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var nextAllowedLoad = GetLocalInt(area, NextAreaLayoutLoadTimestampVariable);
            if (nextAllowedLoad > now)
            {
                var secondsRemaining = nextAllowedLoad - now;
                Instructions = $"Please wait {secondsRemaining}s before loading another layout.";
                InstructionColor = GuiColor.Red;
                return;
            }

            SetLocalInt(area, NextAreaLayoutLoadTimestampVariable, (int)now + LayoutLoadCooldownSeconds);

            var layout = DB.Get<DMAreaPlaceableLayout>(_layoutIds[_selectedLayoutIndex]);
            if (layout?.Entries == null || layout.Entries.Count <= 0)
            {
                Instructions = "Selected layout is empty or missing.";
                InstructionColor = GuiColor.Red;
                return;
            }

            var groupedPlaceables = new Dictionary<string, List<uint>>();
            var dungeonMaster = Player;
            if (GetIsDMPossessed(dungeonMaster))
            {
                var master = GetMaster(dungeonMaster);
                if (GetIsObjectValid(master))
                    dungeonMaster = master;
            }

            var dmId = GetObjectUUID(dungeonMaster);
            var dmName = GetName(dungeonMaster);
            for (var obj = GetFirstObjectInArea(area); GetIsObjectValid(obj); obj = GetNextObjectInArea(area))
            {
                if (GetObjectType(obj) != ObjectType.Placeable)
                    continue;
                if (!DMActions.IsDMSpawnedObject(obj))
                    continue;

                var key = $"{GetTag(obj)}|{GetResRef(obj)}";
                if (!groupedPlaceables.ContainsKey(key))
                    groupedPlaceables[key] = new List<uint>();

                groupedPlaceables[key].Add(obj);
            }

            var movedCount = 0;
            var spawnedCount = 0;
            var skippedCount = 0;
            foreach (var entry in layout.Entries)
            {
                if (entry == null || entry.MatchIndex < 0 || string.IsNullOrWhiteSpace(entry.Resref))
                {
                    skippedCount++;
                    continue;
                }

                var key = $"{entry.Tag}|{entry.Resref}";
                if (!groupedPlaceables.TryGetValue(key, out var keyedPlaceables))
                {
                    keyedPlaceables = new List<uint>();
                    groupedPlaceables[key] = keyedPlaceables;
                }

                // If this keyed position doesn't exist yet, spawn a new placeable for it.
                var spawnAttempts = 0;
                while (entry.MatchIndex >= keyedPlaceables.Count && spawnAttempts < MaxSpawnAttemptsPerEntry)
                {
                    var location = Location(area, new Vector3(entry.X, entry.Y, entry.Z), entry.Facing);
                    var spawn = CreateObject(ObjectType.Placeable, entry.Resref, location);
                    if (!GetIsObjectValid(spawn))
                    {
                        skippedCount++;
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(entry.Tag))
                        SetTag(spawn, entry.Tag);

                    DMActions.MarkDMSpawnedObject(spawn, dungeonMaster);

                    keyedPlaceables.Add(spawn);
                    spawnedCount++;
                    spawnAttempts++;
                }

                if (entry.MatchIndex >= keyedPlaceables.Count)
                {
                    skippedCount++;
                    continue;
                }

                var placeable = keyedPlaceables[entry.MatchIndex];
                if (!GetIsObjectValid(placeable))
                {
                    skippedCount++;
                    continue;
                }

                ObjectPlugin.SetPosition(placeable, new Vector3(entry.X, entry.Y, entry.Z));
                AssignCommand(placeable, () => SetFacing(entry.Facing));
                movedCount++;
            }

            RebuildEntries();
            Search();
            Instructions = $"Loaded '{layout.Name}': moved {movedCount}, spawned {spawnedCount}, skipped {skippedCount}.";
            InstructionColor = movedCount + spawnedCount > 0 ? GuiColor.Green : GuiColor.Red;
        };

        public Action OnDeleteLayout() => () =>
        {
            if (!IsLayoutSelected || _selectedLayoutIndex < 0 || _selectedLayoutIndex >= _layoutIds.Count)
            {
                Instructions = "Select a layout to delete.";
                InstructionColor = GuiColor.Red;
                return;
            }

            var layoutId = _layoutIds[_selectedLayoutIndex];
            var selectedLayoutName = LayoutNames[_selectedLayoutIndex];

            ShowModal($"Delete layout '{selectedLayoutName}' for this area?", () =>
            {
                if (!DB.Exists<DMAreaPlaceableLayout>(layoutId))
                {
                    LayoutName = string.Empty;
                    LoadLayouts();
                    Instructions = "Layout was already deleted.";
                    InstructionColor = GuiColor.Red;
                    return;
                }

                try
                {
                    DB.Delete<DMAreaPlaceableLayout>(layoutId);
                }
                catch
                {
                    LayoutName = string.Empty;
                    LoadLayouts();
                    Instructions = "Unable to delete layout. It may have already been removed.";
                    InstructionColor = GuiColor.Red;
                    return;
                }

                LayoutName = string.Empty;
                LoadLayouts();
                Instructions = "Layout deleted.";
                InstructionColor = GuiColor.Green;
            });
        };

        private void AdjustPosition(float x, float y, float z)
        {
            var placeable = GetSelectedPlaceable();
            if (!GetIsObjectValid(placeable))
                return;
            if (!EnsureEditingOwnership(placeable))
                return;

            var position = GetPosition(placeable);
            position.X += x;
            position.Y += y;
            position.Z += z;

            ObjectPlugin.SetPosition(placeable, position);
        }

        public Action OnYAxisUp() => () =>
        {
            AdjustPosition(0f, 0.1f, 0f);
        };

        public Action OnYAxisDown() => () =>
        {
            AdjustPosition(0f, -0.1f, 0f);
        };

        public Action OnXAxisUp() => () =>
        {
            AdjustPosition(0.1f, 0f, 0f);
        };

        public Action OnXAxisDown() => () =>
        {
            AdjustPosition(-0.1f, 0f, 0f);
        };

        public Action OnZAxisUp() => () =>
        {
            AdjustPosition(0f, 0f, 0.1f);
        };

        public Action OnZAxisDown() => () =>
        {
            AdjustPosition(0f, 0f, -0.1f);
        };

        public Action OnZAxisReset() => () =>
        {
            var placeable = GetSelectedPlaceable();
            if (!GetIsObjectValid(placeable))
                return;
            if (!EnsureEditingOwnership(placeable))
                return;

            var location = GetLocation(placeable);
            var position = GetPosition(placeable);
            var z = GetGroundHeight(location);

            if (z == -6f)
                return;

            position.Z = z;
            ObjectPlugin.SetPosition(placeable, position);
        };

        private static float NormalizeFacing(float facing)
        {
            return GameMath.NormalizeDegrees(facing);
        }

        private void AdjustFacing(float facing)
        {
            var placeable = GetSelectedPlaceable();
            if (!GetIsObjectValid(placeable))
                return;
            if (!EnsureEditingOwnership(placeable))
                return;

            facing = NormalizeFacing(facing);
            AssignCommand(placeable, () => SetFacing(facing));
        }

        public Action OnRotateClockwise() => () =>
        {
            var placeable = GetSelectedPlaceable();
            if (!GetIsObjectValid(placeable))
                return;

            var facing = GetFacing(placeable) + 20f;
            AdjustFacing(facing);
        };

        public Action OnRotateCounterClockwise() => () =>
        {
            var placeable = GetSelectedPlaceable();
            if (!GetIsObjectValid(placeable))
                return;

            var facing = GetFacing(placeable) - 20f;
            AdjustFacing(facing);
        };

        public Action OnSetFacingNorth() => () =>
        {
            AdjustFacing(90f);
        };

        public Action OnSetFacingSouth() => () =>
        {
            AdjustFacing(270f);
        };

        public Action OnSetFacingEast() => () =>
        {
            AdjustFacing(0f);
        };

        public Action OnSetFacingWest() => () =>
        {
            AdjustFacing(180f);
        };

        public Action OnSaveChanges() => () =>
        {
            var placeable = GetSelectedPlaceable();
            if (!GetIsObjectValid(placeable))
                return;

            var position = GetPosition(placeable);
            var facing = GetFacing(placeable);
            var targetName = _entryDisplayNames[SelectedEntryIndex];
            Instructions = $"Changes are live for {targetName} ({position.X:0.00}, {position.Y:0.00}, {position.Z:0.00}, {facing:0.0}). Use Save Layout to persist.";
            InstructionColor = GuiColor.Green;
        };

        public Action OnDeletePlaceable() => () =>
        {
            var placeable = GetSelectedPlaceable();
            if (!GetIsObjectValid(placeable))
                return;
            if (!EnsureEditingOwnership(placeable))
                return;

            var selectedPlaceableName = _entryDisplayNames[SelectedEntryIndex];
            ShowModal($"Delete placeable '{selectedPlaceableName}'?", () =>
            {
                ClearHighlight();
                DestroyObject(placeable);
                RebuildEntries();
                Search();
                Instructions = $"Deleted: {selectedPlaceableName}";
                InstructionColor = GuiColor.Green;
            });
        };

        public Action OnResetArea() => () =>
        {
            ShowModal("Delete all DM-spawned placeables in this area?", () =>
            {
                ClearHighlight();
                _allEntries.ForEach((placeable) => DestroyObject(placeable));
                RebuildEntries();
                Search();
                Instructions = "Deleted all DM-spawned placeables.";
                InstructionColor = GuiColor.Green;
            });
        };
    }
}
