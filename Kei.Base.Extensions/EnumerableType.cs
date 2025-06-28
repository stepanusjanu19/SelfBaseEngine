namespace Kei.Base.Extensions
{
    public enum FilterOperator
    {
        Equal,
        NotEqual,
        Contains,
        StartsWith,
        EndsWith,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        In,
        NotIn,
        IsNull,
        IsNotNull,
        Between,
        NotLike
    }
    public enum PaginationMode
    {
        WithQuery,
        Project,
        SelfWithQuery,
        Self
    }

    
}