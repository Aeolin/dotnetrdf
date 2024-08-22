using System.Runtime.CompilerServices;
using VDS.RDF.Query.Algebra;

namespace VDS.RDF.Query.Pull.Algebra;

internal abstract class AbstractAsyncJoinEvaluation : IAsyncEvaluation
{
    private readonly IAsyncEvaluation _lhs;
    private readonly IAsyncEvaluation _rhs;
    protected bool _lhsHasMore;
    protected bool _rhsHasMore;
    private IAsyncEnumerator<ISet>? _lhsResults;
    private IAsyncEnumerator<ISet>? _rhsResults;

    protected AbstractAsyncJoinEvaluation(IAsyncEvaluation lhs, IAsyncEvaluation rhs)
    {
        _lhs = lhs;
        _rhs = rhs;
    }

    public async IAsyncEnumerable<ISet> Evaluate(PullEvaluationContext context, ISet? input, IRefNode? activeGraph = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _lhsHasMore = true;
        _rhsHasMore = true;
        _lhsResults = _lhs.Evaluate(context, input, activeGraph).GetAsyncEnumerator(cancellationToken);
        _rhsResults = _rhs.Evaluate(context, input, activeGraph).GetAsyncEnumerator(cancellationToken);
        Task<bool> lhsMoveNext = _lhsResults.MoveNextAsync().AsTask();
        Task<bool> rhsMoveNext = _rhsResults.MoveNextAsync().AsTask();
        IAsyncEnumerable<ISet>? joinEnumerator = null;
        do
        {
            if (joinEnumerator != null)
            {
                await foreach (ISet? joinResult in joinEnumerator)
                {
                    if (joinResult != null)
                    {
                        yield return joinResult;
                    }
                }

                joinEnumerator = null;
            }

            if (_lhsHasMore)
            {
                if (_rhsHasMore)
                {
                    var completed = Task.WaitAny(lhsMoveNext, rhsMoveNext);
                    if (completed == 0)
                    {
                        _lhsHasMore = lhsMoveNext.Result;
                        if (_lhsHasMore)
                        {
                            foreach (ISet p in ProcessLhs(context, _lhsResults.Current, activeGraph))
                            {
                                yield return p;
                            }

                            lhsMoveNext = _lhsResults.MoveNextAsync().AsTask();
                        }
                        else
                        {
                            IEnumerable<ISet>? moreResults =  OnLhsDone(context);
                            if (moreResults != null)
                            {
                                foreach (ISet r in moreResults) { yield return r; }
                            }
                        }
                    }
                    else
                    {
                        _rhsHasMore = rhsMoveNext.Result;
                        if (_rhsHasMore)
                        {
                            foreach (ISet p in ProcessRhs(context, _rhsResults.Current, activeGraph))
                            {
                                yield return p;
                            }

                            rhsMoveNext = _rhsResults.MoveNextAsync().AsTask();
                        }
                        else
                        {
                            IEnumerable<ISet>? moreResults = OnRhsDone(context);
                            if (moreResults != null)
                            {
                                foreach (ISet r in moreResults) { yield return r; }
                            }
                        }
                    }
                }
                else
                {
                    _lhsHasMore = await lhsMoveNext;
                    if (_lhsHasMore)
                    {
                        foreach (ISet p in ProcessLhs(context, _lhsResults.Current, activeGraph))
                        {
                            yield return p;
                        }

                        lhsMoveNext = _lhsResults.MoveNextAsync().AsTask();
                    }
                    else
                    {
                        IEnumerable<ISet>? moreResults = OnLhsDone(context);
                        if (moreResults != null)
                        {
                            foreach (ISet r in moreResults) { yield return r; }
                        }
                    }
                }
            }
            else if (_rhsHasMore)
            {
                _rhsHasMore = await rhsMoveNext;
                if (_rhsHasMore)
                {
                    foreach (ISet p in ProcessRhs(context, _rhsResults.Current, activeGraph))
                    {
                        yield return p;
                    }

                    rhsMoveNext = _rhsResults.MoveNextAsync().AsTask();
                }
                else
                {
                    IEnumerable<ISet>? moreResults = OnRhsDone(context);
                    if (moreResults != null)
                    {
                        foreach (ISet r in moreResults) { yield return r; }
                    }
                }
            }
        } while (_lhsHasMore || _rhsHasMore);
    }

    protected abstract IEnumerable<ISet> ProcessLhs(PullEvaluationContext context, ISet lhSolution, IRefNode? activeGraph);
    protected abstract IEnumerable<ISet> ProcessRhs(PullEvaluationContext context, ISet rhSolution, IRefNode? activeGraph);
    protected abstract IEnumerable<ISet>? OnLhsDone(PullEvaluationContext context);
    protected abstract IEnumerable<ISet>? OnRhsDone(PullEvaluationContext context);
}