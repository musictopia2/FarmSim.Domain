namespace FarmSim.Domain.Services.Scenarios;
public class ScenarioManager(InventoryManager inventoryManager,
    params BasicList<IScenarioResettable> resets
    )
{
    private int _trackedSeq = 0;
    private ScenarioProfileModel? _currentProfile = null;
    private IScenarioProfile _scenarioProfileDb = null!;
    private IScenarioGenerationService _scenarioGenerationService = null!;
    private BasicList<ScenarioInstance> _tasks = [];
    public event Action? OnUpdated;
    Dictionary<string, int> _baseLine = [];
    public async Task SetStyleContextAsync(ScenarioServicesContext context, FarmKey farm)
    {
        
        _scenarioProfileDb = context.ScenarioProfile;
        _currentProfile = await context.ScenarioProfile.LoadAsync();
        if (_currentProfile is null)
        {
            return; //there is none.
        }
        _scenarioGenerationService = context.ScenarioGeneration;
        _baseLine = await context.InventoryStarterRepository.GetBaseLineAsync(farm);
        _tasks = _currentProfile.Tasks;
    }
    public EnumScenarioStatus GetStatus
    {
        get
        {
            if (_currentProfile is null)
            {
                throw new CustomBasicException("Only coins should get the status");
            }

            return _currentProfile.Status;
        }
    }
    public string TimeLeft()
    {
        if (_currentProfile is null)
        {
            return "";
        }

        // If not in cooldown, there isn't "time left" in the cooldown sense.
        if (_currentProfile.Status != EnumScenarioStatus.Cooldown)
        {
            // pick whichever is best for your UI:
            // return "";
            return "Ready";
        }

        if (_currentProfile.LastCompleted is null)
        {
            // cooldown with no timestamp is inconsistent, but don't crash UI
            return "Ready";
        }

        DateTime now = DateTime.Now;
        DateTime next = _currentProfile.LastCompleted.Value + _currentProfile.TimeBetween;

        if (now >= next)
        {
            return "Ready";
        }

        TimeSpan remaining = next - now;
        return remaining.GetTimeString; // you already have this extension
    }

    private async Task TryToCreateTasksAsync()
    {
        if (_currentProfile is null)
        {
            return;
        }
        if (_currentProfile.Status == EnumScenarioStatus.Progress)
        {
            return; //nothing to create.
        }
        if (_tasks.Count > 0)
        {
            return; //because there are tasks to do.
        }
        if (_currentProfile.Status == EnumScenarioStatus.WaitingToClaim)
        {
            return; //waiting to claim.
        }
        if (_currentProfile.Status == EnumScenarioStatus.None)
        {
            _tasks = _scenarioGenerationService.GetScenarios();
            _trackedSeq = _tasks.Count == 0 ? 0 : _tasks.Max(x => x.Order);
            _currentProfile.Tasks = _tasks;
            _currentProfile.Status = EnumScenarioStatus.Progress;
            await UpdateAsync();
            return;
        }
    }
    private bool CanRemoveFromCooldown()
    {
        if (_currentProfile is null)
        {
            return false;
        }

        // Only meaningful if in cooldown
        if (_currentProfile.Status != EnumScenarioStatus.Cooldown)
        {
            return true; // not cooling down, so "can remove" is effectively yes
        }

        if (_currentProfile.LastCompleted is null)
        {
            // If we somehow got into cooldown without a completion time, treat as ready
            return true;
        }
        DateTime now = DateTime.Now;
        DateTime next = _currentProfile.LastCompleted.Value + _currentProfile.TimeBetween;
        return now >= next;
    }
    private async Task UpdateAsync()
    {
        if (_currentProfile is null)
        {
            return;
        }
        _currentProfile.Tasks = _tasks;
        await _scenarioProfileDb.SaveAsync(_currentProfile);
        OnUpdated?.Invoke();
    }
    //ui calls this.
    public async Task LoadAsync()
    {
        if (_currentProfile is null)
        {
            return;
        }

        // If cooldown is over, flip to None so UI changes layout
        if (_currentProfile.Status == EnumScenarioStatus.Cooldown && CanRemoveFromCooldown())
        {
            _currentProfile.Status = EnumScenarioStatus.None;
            await UpdateAsync();
        }

        await TryToCreateTasksAsync(); // this will generate only when Status == None AND tasks empty
    }
    public BasicList<ScenarioInstance> ShowCurrentScenarios(int max = 3)
    {
        if (max <= 0)
        {
            return [];
        }

        // 1) Tracked first (most recent tracked first) - KEEP DUPLICATES
        var tracked = _tasks
            .Where(x => x.Tracked)
            .OrderByDescending(x => x.Order)
            .ToBasicList();

        // If tracked alone satisfies the UI, show exactly what they chose (even duplicates).
        if (tracked.Count >= max)
        {
            return tracked.Take(max).ToBasicList();
        }

        // Start result with tracked items (including duplicates)
        var result = new BasicList<ScenarioInstance>();
        result.AddRange(tracked);

        // Track which "Item" values are already represented by tracked items
        // so suggestions don't duplicate them.
        var usedItems = new HashSet<string>(
            tracked.Select(x => x.Item),
            StringComparer.OrdinalIgnoreCase
        );

        int remaining = max - result.Count;
        if (remaining <= 0)
        {
            return result.Take(max).ToBasicList();
        }

        // 2) Suggestion candidates: not tracked, and not already represented in tracked items
        // We ALSO want suggestions to avoid duplicates among themselves (by Item).
        var suggestions = new BasicList<ScenarioInstance>();

        // Prefer: unseen first, then some stable sort (Order desc is fine if used)
        var orderedCandidates = _tasks
            .Where(x => !x.Tracked)
            .Where(x => !usedItems.Contains(x.Item))
            .OrderBy(x => x.Seen)                 // false (unseen) first
            .ThenByDescending(x => x.Order)       // more recent/important next
            .ThenBy(x => x.Item)                  // stability
            .ToBasicList();

        foreach (var candidate in orderedCandidates)
        {
            if (suggestions.Count >= remaining)
            {
                break;
            }

            // Avoid duplicates among suggestions by Item
            if (usedItems.Add(candidate.Item))
            {
                suggestions.Add(candidate);
            }
        }

        result.AddRange(suggestions);
        return result.Take(max).ToBasicList();
    }
    public int Reward => _currentProfile!.Rewards;
    public BasicList<ScenarioInstance> GetAllIncompleteTasks()
        => _tasks.ToBasicList();
    public bool CanCompleteTask(ScenarioInstance task) => inventoryManager.Has(task.Item, task.Required);
    public async Task CompleteTaskAsync(ScenarioInstance task)
    {
        if (_currentProfile is null)
        {
            return;
        }
        if (CanCompleteTask(task) == false)
        {
            throw new CustomBasicException("Unable to complete quest.   Should had called CanCompleteTask first");
        }
        inventoryManager.Consume(task.Item, task.Required);
        _tasks.RemoveAllOnly(x => x.ScenarioId == task.ScenarioId);
        if (_tasks.Count == 0)
        {
            _currentProfile.Status = EnumScenarioStatus.WaitingToClaim;
        }
        await UpdateAsync();
    }
    public async Task SetTrackedAsync(ScenarioInstance task, bool tracked, int maxTracked = 3)
    {
        if (tracked == false)
        {
            task.Tracked = false;
            task.Order = 0;
            await UpdateAsync();
            return;
        }

        // If already tracked, just "refresh" its recency
        if (task.Tracked)
        {
            task.Order = ++_trackedSeq;
            await UpdateAsync();
            return;
        }

        // If at cap, auto-untrack the oldest tracked quest
        var trackedList = _tasks
            .Where(x => x.Tracked)
            .OrderBy(x => x.Order) // oldest first
            .ToBasicList();

        if (trackedList.Count >= maxTracked)
        {
            var toUntrack = trackedList[0];
            toUntrack.Tracked = false;
            toUntrack.Order = 0;
        }

        // Track the new one as most recent
        task.Tracked = true;
        task.Order = ++_trackedSeq;
        await UpdateAsync();
    }
    public async Task MarkAllIncompleteSeenAsync()
    {
        bool changed = false;

        foreach (var task in _tasks)
        {
            if (task.Seen == false)
            {
                task.Seen = true;
                changed = true;
            }
        }
        if (changed)
        {
            await UpdateAsync();
        }
    }
    private void ResetFarm()
    {
        inventoryManager.ResetInventory(_baseLine);
        resets.ForEach(r => r.ResetForScenario());
        //cropManager.ResetAllCropsToEmpty();
        //treeManager.ResetAllTreesToIdle();
        //animalManager.ResetAllAnimalsToIdle();
        //workshopManager.ResetAllWorkshopQueues();
        //worksiteManager.ResetAll();
    }

    public async Task ClaimRewardAsync(INavigation nav, FarmKey farm, FarmTransferService transfer, IToast toast)
    {
        if (_currentProfile is null)
        {
            return;
        }

        int reward = _currentProfile.Rewards;

        // Pay first so the player never loses the reward if something crashes after.
        await transfer.AddCoinFromScenarioCompletionAsync(farm, reward, toast);

        // Now commit scenario completion + cooldown
        _currentProfile.LastCompleted = DateTime.Now;
        _tasks = [];
        _currentProfile.Status = EnumScenarioStatus.Cooldown;

        ResetFarm();
        await UpdateAsync();

        nav.NavigateTo(farm);
    }
}