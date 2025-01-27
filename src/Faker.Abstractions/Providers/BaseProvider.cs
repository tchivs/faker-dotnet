﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

// ReSharper disable once CheckNamespace
namespace Faker
{
    public interface IProvider : IDisposable
    {
    }
    public interface IProvider<out TResult> : IProvider
    {
        TResult this[string name] { get; }
    }

    public abstract class BaseProvider : IProvider
    {
        protected CultureInfo CultureInfo { get; }
        protected IGenerator Generator { get; }

        protected ProviderOptions ProviderOptions { get; }

        protected BaseProvider(CultureInfo cultureInfo, IGenerator generator, ProviderOptions options)
        {
            CultureInfo = cultureInfo;
            Generator = generator;
            this.ProviderOptions = options;
        }
        public abstract void Dispose();
    }
    public abstract class BaseProvider<TResult> : BaseProvider, IProvider<TResult>
    {
        /// <summary>
        /// 包含权重的随机选择器
        /// </summary>
        protected Func<SortedList<TResult, double>, int, IEnumerable<TResult>> Selecter { get; }
        /// <summary>
        /// 随机列表选择器
        /// </summary>
        protected Func<IList<TResult>, int, IEnumerable<TResult>> Choices { get; }
        /// <summary>
        /// 随机单个选择器
        /// </summary>
        protected Func<SortedList<TResult, double>, TResult> ChoicesOneFunc { get; }
        /// <summary>
        /// weight cache
        /// </summary>
        protected Dictionary<int, (double, int)[]> Weights { get; set; } = new Dictionary<int, (double, int)[]>();
        /// <summary>
        /// 权重选择器
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        protected virtual IEnumerable<TResult> WeightingSelecter(SortedList<TResult, double> elements, int length)
        {
            foreach (var idx in this.Generator.GetRandomIndeiesByWidths(elements.Values))
            {
                yield return elements.Keys[idx];
            }
        }
        /// <summary>
        /// 简单选择器
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        protected IEnumerable<TResult> SelecterSample(SortedList<TResult, double> elements, int length)
        {
            if (this.ProviderOptions.Unique && length > elements.Count)
            {
                throw new Exception("Sample length cannot be longer than the number of unique elements to pick from.");
            }
            return this.Choices.Invoke(elements.Keys, length);
        }
        protected TResult ChoicesOneSample(IList<TResult> elements)
        {
            return elements[this.Generator.GetRandomIndex(elements.Count)];
        }
        protected TResult ChoicesOneByWeight(SortedList<TResult, double> elements)
        {
            var key = elements.GetHashCode();
            if (!Weights.TryGetValue(key, out var w))
            {
                w = this.Generator.GetWeightIndex(elements.Values);
                Weights.Add(key, w);
            }
            return elements.Keys[this.Generator.GetRandomIndexByWidths(w)];
        }
        protected virtual IEnumerable<TResult> ChoicesDistributionUnique(IList<TResult> elements,
            int length)
        {
            if (length == 1)
            {
                return ChoicesDistribution(elements, length);
            }
            return this.Generator.Unique(elements, length);
        }
        protected virtual IEnumerable<TResult> ChoicesDistribution(IList<TResult> elements, int length)
        {
            return this.Generator.Sample(elements, length);
        }
        public TResult this[string name] => GetOrCreateIndexFunction(name).Invoke();
        readonly Dictionary<string, Func<TResult>> _lambdaCache = new Dictionary<string, Func<TResult>>();
        protected Func<TResult> GetOrCreateIndexFunction(string methodName)
        {
            if (!_lambdaCache.TryGetValue(methodName ?? throw new NullReferenceException(nameof(methodName)), out var lambda))
            {
                var method = this.GetType().GetMethod(methodName);
                if (method == null)
                {
                    throw new MissingMethodException(nameof(methodName));
                }

                var x = Expression.Constant(this);
                var call = Expression.Call(x, method);
                var expression = Expression.Lambda<Func<TResult>>(call);

                lambda = expression.Compile();
                _lambdaCache.Add(methodName, lambda);
            }
            return lambda;

        }
        public override void Dispose()
        {
            this._lambdaCache.Clear();
            Weights.Clear();
        }
        protected BaseProvider(CultureInfo cultureInfo, IGenerator generator, ProviderOptions options) : base(cultureInfo, generator, options)
        {
            if (this.ProviderOptions.UseWeighting)
            {
                Selecter = WeightingSelecter;
                ChoicesOneFunc = ChoicesOneByWeight;
            }
            else
            {
                Selecter = SelecterSample;
                ChoicesOneFunc = (e) =>
                {
                    return this.ChoicesOneSample(e.Keys);
                };
            }
            if (this.ProviderOptions.Unique)
            {
                Choices = this.ChoicesDistributionUnique;
            }
            else
            {
                Choices = this.ChoicesDistribution;
            }
        }
        protected TResult this[IList<TResult> elements] { get => this.ChoicesOneSample(elements); }
        protected TResult this[SortedList<TResult, double> elements] { get => this.ChoicesOneFunc(elements); }
    }
}