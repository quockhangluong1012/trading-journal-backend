using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace TradingJournal.Shared.Extensions;

public static class QueryableExtensions
{
    public static async Task<IReadOnlyCollection<T>> ToPaginatedListAsync<T>(
        this IQueryable<T> query,
        int pageIndex,
        int pageSize,
        Expression<Func<T, object>>? orderByDescending = null,
        Expression<Func<T, object>>? orderByAscending = null
    )
    {
        if (orderByDescending != null)
        {
            query = query
                .OrderByDescending(orderByDescending);
        }
        else if (orderByAscending != null)
        {
            query = query
                .OrderBy(orderByAscending);
        }

        return await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}
