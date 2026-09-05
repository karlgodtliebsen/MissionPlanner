namespace MissionPlanner.App.Utilities.Dispatching;

/// <summary>
/// Dispatches actions to the UI thread.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Checks if the current thread has access to the UI thread.
    /// </summary>
    /// <returns>True if the current thread has access to the UI thread; otherwise, false.</returns>
    bool CheckAccess();

    /// <summary>
    /// Dispatches an action to be executed on the UI thread asynchronously.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DispatchAsync(Action action);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="action"></param>
    void Dispatch(Action action);

    /// <summary>
    /// Dispatches a function to be executed on the UI thread asynchronously and returns a result.
    /// </summary>
    /// <param name="action">The function to execute.</param>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <returns>A task that represents the asynchronous operation and contains the result.</returns>   
    Task<T> DispatchAsync<T>(Func<T> action);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="action"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T Dispatch<T>(Func<T> action);


    /// <summary>
    /// Dispatches a function to be executed on the UI thread asynchronously and returns a task.
    /// </summary>
    /// <param name="action">The function to execute.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DispatchAsync(Func<Task> action);

    /// <summary>
    /// Dispatches a function to be executed on the UI thread asynchronously and returns a task with a result.
    /// </summary>
    /// <param name="action">The function to execute.</param>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <returns>A task that represents the asynchronous operation and contains the result.</returns>
    Task<T> DispatchAsync<T>(Func<Task<T>> action);
}
