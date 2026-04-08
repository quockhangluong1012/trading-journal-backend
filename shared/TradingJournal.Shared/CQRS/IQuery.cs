using MediatR;

namespace TradingJournal.Shared.CQRS;

public interface IQuery<out TResponse> : IRequest<TResponse>
    where TResponse : notnull
{
}