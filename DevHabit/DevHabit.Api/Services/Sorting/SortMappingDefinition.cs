using System.Diagnostics.CodeAnalysis;

namespace DevHabit.Api.Services.Sorting;

#pragma warning disable S2326
public sealed class SortMappingDefinition<TSource, TDestination> : ISortMappingDefinition
#pragma warning restore S2326
{
    private readonly SortMapping[] _mappings;

    public required SortMapping[] Mappings
    {
        get => _mappings;
        [MemberNotNull(nameof(_mappings))]
        init => _mappings = value;
    }
}
