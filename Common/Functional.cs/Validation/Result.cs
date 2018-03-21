﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Functional
{
    using System.IO;
    using static ResultEx;

    public static partial class ResultEx
    {
        public static Result.Ok<T> Ok<T>(T value) => new Result.Ok<T>(value);
        public static Result.Failure Failure(Exception error) => new Result.Failure(error);
    }

    //  The generic Result<T> type in C#
    public struct Result<T>
    {
        public T Ok { get; }
        public Exception Error { get; }

        public bool IsFailed { get => Error != null; }
        public bool IsOk => !IsFailed;

        public Result(T ok)
        {
            Ok = ok;
            Error = default(Exception);
        }
        public Result(Exception error)
        {
            Error = error;
            Ok = default(T);
        }

        public R Match<R>(Func<T, R> okMap, Func<Exception, R> failureMap)
              => IsOk ? okMap(Ok) : failureMap(Error);

        public void Match(Action<T> okAction, Action<Exception> errorAction)
        {
            if (IsOk) okAction(Ok); else errorAction(Error);
        }

        public static implicit operator Result<T>(T ok) => new Result<T>(ok);
        public static implicit operator Result<T>(Exception error) => new Result<T>(error);

        public static implicit operator Result<T>(Result.Ok<T> ok) => new Result<T>(ok.Value);
        public static implicit operator Result<T>(Result.Failure error)
              => new Result<T>(error.Error);
    }


    public static class Result
    {
        public struct Ok<L>
        {
            internal L Value { get; }
            internal Ok(L value) { Value = value; }

        }

        public struct Failure
        {
            internal Exception Error { get; }
            internal Failure(Exception error) { Error = error; }
        }
    }

    //  Task<Result<T>> helper functions for compositional semantic
    public static class ResultExtensions
    {
        public static async Task<Result<T>> TryCatch<T>(Func<Task<T>> func)
        {
            try
            {
                return await func();
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public static Task<Result<T>> TryCatch<T>(Func<T> func)
        {
            return TryCatch(() => Task.FromResult(func()));
        }

        public static async Task<Result<R>> SelectMany<T, R>(this Task<Result<T>> resultTask, Func<T, Task<Result<R>>> func)
        {
            Result<T> result = await resultTask.ConfigureAwait(false);
            if (result.IsFailed)
                return result.Error;
            return await func(result.Ok);
        }

        public static async Task<Result<R>> Select<T, R>(this Task<Result<T>> resultTask, Func<T, Task<R>> func)
        {
            Result<T> result = await resultTask.ConfigureAwait(false);
            if (result.IsFailed)
                return result.Error;
            return await func(result.Ok).ConfigureAwait(false);
        }
        public static async Task<Result<R>> Match<T, R>(this Task<Result<T>> resultTask, Func<T, Task<R>> actionOk, Func<Exception, Task<R>> actionError)
        {
            Result<T> result = await resultTask.ConfigureAwait(false);
            if (result.IsFailed)
                return await actionError(result.Error);
            return await actionOk(result.Ok);
        }



        public static async Task<Result<T>> ToResult<T>(this Task<Option<T>> optionTask)
           where T : class
        {
            Option<T> opt = await optionTask.ConfigureAwait(false);

            if (opt.IsSome()) return Ok(opt.Value);
            return new Exception();
        }

        public static async Task<Result<R>> OnSuccess<T, R>(this Task<Result<T>> resultTask, Func<T, Task<R>> func)
        {
            Result<T> result = await resultTask.ConfigureAwait(false);

            if (result.IsFailed)
                return result.Error;
            return await func(result.Ok).ConfigureAwait(false);
        }

        public static async Task<Result<T>> OnFailure<T>(this Task<Result<T>> resultTask, Func<Task> func)
        {
            Result<T> result = await resultTask.ConfigureAwait(false);

            if (result.IsFailed)
                await func().ConfigureAwait(false);
            return result;
        }

        public static async Task<Result<R>> Bind<T, R>(this Task<Result<T>> resultTask, Func<T, Task<Result<R>>> func)
        {
            Result<T> result = await resultTask.ConfigureAwait(false);

            if (result.IsFailed)
                return result.Error;
            return await func(result.Ok).ConfigureAwait(false);
        }

        public static async Task<Result<R>> Map<T, R>(this Task<Result<T>> resultTask, Func<T, Task<R>> func)
        {
            Result<T> result = await resultTask.ConfigureAwait(false);

            if (result.IsFailed)
                return result.Error;

            return await TryCatch(() => func(result.Ok));
        }

        public static async Task<Result<R>> Match<T, R>(this Task<Result<T>> resultTask, Func<T, R> actionOk, Func<Exception, R> actionError)
        {
            Result<T> result = await resultTask.ConfigureAwait(false);

            if (result.IsFailed)
                return actionError(result.Error);
            return actionOk(result.Ok);
        }

        public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> resultTask, Func<T, Task<bool>> predicate, string errorMessage)
        {
            Result<T> result = await resultTask.ConfigureAwait(false);

            if (result.IsFailed || !await predicate(result.Ok).ConfigureAwait(false)) return result.Error;
            return result.Ok;
        }

        public static async Task<Result<T>> Tee<T>(this Task<Result<T>> resultTask, Func<T, Task> action)
        {
            Result<T> result = await resultTask.ConfigureAwait(false);

            if (result.IsOk)
                await action(result.Ok).ConfigureAwait(false);
            return result;
        }
    }

    public static class ResultExt
    {
        public static R Match<T, R>(this Result<T> result, Func<T, R> okMap, Func<Exception, R> failureMap)
            => result.IsOk ? okMap(result.Ok) : failureMap(result.Error);

        public static Result<R> OnSuccess<T, R>(this Result<T> result, Func<T, R> map)
        {
            if (result.IsFailed)
                return Failure(result.Error);
            return Ok(map(result.Ok));
        }

        public static Result<T> OnFailure<T>(this Result<T> result, Action<Exception> action)
        {
            if (result.IsFailed)
                action(result.Error);
            return result;
        }

        public static Result<R> Bind<T, R>(this Result<T> result, Func<T, Result<R>> map)
        {
            if (result.IsFailed)
                return result.Error;
            return map(result.Ok);
        }

        public static Result<R> Map<L, R>(Result<L> result, Func<L, R> map)
        {
            if (result.IsOk) return map(result.Ok);
            return result.Error;
        }

        public static Result<R> BiMap<T, R>(this Result<T> result, Func<T, R> mapOk, Func<Exception, R> mapError)
        {
            if (result.IsFailed)
                return mapError(result.Error);
            return mapOk(result.Ok);
        }

        public static Result<R> BiMap<T, R>(this Result<T> result, Func<T, R> mapOk, Action<Exception> action)
        {
            if (result.IsFailed)
            {
                action(result.Error);
                return result.Error;
            }
            return mapOk(result.Ok);
        }
    }

}