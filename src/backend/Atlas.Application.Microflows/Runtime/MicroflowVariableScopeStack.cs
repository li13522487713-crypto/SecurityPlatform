namespace Atlas.Application.Microflows.Runtime;

public sealed class MicroflowVariableScopeStack
{
    private readonly List<MicroflowVariableScopeFrame> _frames = [];

    public MicroflowVariableScopeStack()
    {
        _frames.Add(new MicroflowVariableScopeFrame
        {
            Kind = MicroflowVariableScopeKind.Global,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    public MicroflowVariableScopeFrame Current => _frames[^1];

    public IReadOnlyList<MicroflowVariableScopeFrame> Frames => _frames;

    public MicroflowVariableScopeFrame Push(MicroflowVariableScopeFrame frame)
    {
        var parent = Current;
        var next = frame with
        {
            ParentId = parent.Id,
            CreatedAt = frame.CreatedAt == default ? DateTimeOffset.UtcNow : frame.CreatedAt
        };
        _frames.Add(next);
        return next;
    }

    public bool Pop(string frameId)
    {
        if (_frames.Count <= 1 || !string.Equals(Current.Id, frameId, StringComparison.Ordinal))
        {
            return false;
        }

        _frames.RemoveAt(_frames.Count - 1);
        return true;
    }

    public bool TryGet(string name, out MicroflowRuntimeVariableValue? value, out MicroflowVariableScopeFrame? scope)
    {
        for (var index = _frames.Count - 1; index >= 0; index--)
        {
            var frame = _frames[index];
            if (frame.Variables.TryGetValue(name, out var found))
            {
                value = found;
                scope = frame;
                return true;
            }
        }

        value = null;
        scope = null;
        return false;
    }

    public bool VisibleNameExists(string name)
        => _frames.Any(frame => frame.Variables.ContainsKey(name));

    public IReadOnlyDictionary<string, MicroflowRuntimeVariableValue> VisibleVariables()
    {
        var result = new Dictionary<string, MicroflowRuntimeVariableValue>(StringComparer.Ordinal);
        foreach (var frame in _frames)
        {
            foreach (var variable in frame.Variables)
            {
                result[variable.Key] = variable.Value;
            }
        }

        return result;
    }
}
