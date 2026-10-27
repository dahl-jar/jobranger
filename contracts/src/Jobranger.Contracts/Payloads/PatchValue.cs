namespace Jobranger.Contracts.Payloads;

public readonly record struct PatchValue<T> {
    public PatchValue(T value) {
        Value = value;
        HasValue = true;
    }

    public bool HasValue { get; }

    public T Value { get; }
}
