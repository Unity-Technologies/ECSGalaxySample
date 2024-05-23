using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public struct AIAction
{
    public float Importance;
    public int ConsiderationsCount;
    public float HighestConsideration;

    public static AIAction New()
    {
        return new AIAction
        {
            Importance = 1f,
            ConsiderationsCount = 0,
            HighestConsideration = 0,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyConsideration(float considerationValue)
    {
        Importance *= considerationValue;
        ConsiderationsCount++;
        HighestConsideration = math.max(HighestConsideration, considerationValue);
    }

    public void Reset()
    {
        this = AIAction.New();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Nullify()
    {
        Importance = 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasConsiderationsAndImportance()
    {
        return ConsiderationsCount > 0 && Importance > 0f;
    }
}

public struct AIProcessor
{
    private int _highestConsiderationsCount;
    private UnsafeList<AIAction> _actions;

    public AIProcessor(int initialActionsCapacity = 64, Allocator allocator = Allocator.Temp)
    {
        _highestConsiderationsCount = default;
        _actions = default;
        Create(initialActionsCapacity, allocator);
    }
    
    public void Create(int initialActionsCapacity = 64, Allocator allocator = Allocator.Temp)
    {
        Dispose();
        _highestConsiderationsCount = 0;
        _actions = new UnsafeList<AIAction>(initialActionsCapacity, allocator);
    }

    public void Dispose(JobHandle dep = default)
    {
        if (_actions.IsCreated)
        {
            _actions.Dispose(dep);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _highestConsiderationsCount = 0;
        _actions.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddAction(AIAction aiAction)
    {
        if (_actions.Length + 1 > _actions.Capacity)
        {
            _actions.Resize(_actions.Capacity * 2, NativeArrayOptions.ClearMemory);
        }

        int actionIndex = _actions.Length;
        _actions.Add(aiAction);
        _highestConsiderationsCount = math.max(aiAction.ConsiderationsCount, _highestConsiderationsCount);

        return actionIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AIAction GetActionAt(int actionIndex)
    {
        return _actions[actionIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetActionAt(int actionIndex, AIAction aiAction)
    {
        _actions[actionIndex] = aiAction;
        _highestConsiderationsCount = math.max(aiAction.ConsiderationsCount, _highestConsiderationsCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ComputeFinalImportances()
    {
        for (int i = 0; i < _actions.Length; i++)
        {
            AIAction aiAction = _actions[i];

            // Actions with no considerations are worth zero
            if (aiAction.ConsiderationsCount == 0)
            {
                aiAction.Importance = 0f;
            }
            // For actions with fewer considerations than the action with the most considerations, apply
            // importance compensation
            else if (aiAction.ConsiderationsCount < _highestConsiderationsCount)
            {
                int considerationsDiff = _highestConsiderationsCount - aiAction.ConsiderationsCount;
                for (int j = 0; j < considerationsDiff; j++)
                {
                    aiAction.Importance *= aiAction.HighestConsideration;
                }
            }

            _actions[i] = aiAction; 
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetActionImportance(int actionIndex)
    {
        AIAction aiAction = _actions[actionIndex];
        return aiAction.Importance;
    }
}
