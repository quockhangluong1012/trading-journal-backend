using MediatR;

namespace TradingJournal.Shared.CQRS;

public interface ICommand : ICommand<Unit>
{
}

public interface ICommand<out TResponse> : IRequest<TResponse>
{
}