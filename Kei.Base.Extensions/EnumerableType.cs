namespace Kei.Base.Extensions
{
    public enum FilterOperator
    {
        // sinle filter
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
        NotLike,
        
        //group filter
        GroupAnd,
        GroupOr,
    }
    
    public enum FilterGroupOperator
    {
        And,
        Or
    }
    
    public enum PaginationMode
    {
        WithQuery,
        Project,
        SelfWithQuery,
        Self
    } 
}