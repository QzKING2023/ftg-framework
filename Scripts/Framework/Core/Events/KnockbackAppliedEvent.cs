#nullable enable

namespace FTG_Framework.Core.Events;

public readonly record struct KnockbackAppliedEvent(int PlayerId, float HorizontalForce, float VerticalForce, float Gravity, float Friction);
