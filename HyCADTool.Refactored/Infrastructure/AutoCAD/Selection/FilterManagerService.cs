using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Selection.Filters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection
{
    /// <summary>
    /// 过滤器管理服务实现
    /// Filter Manager Service Implementation
    /// </summary>
    public class FilterManagerService : IFilterManagerService
    {
        private readonly Dictionary<string, IEntityFilter> _filterRegistry;
        private readonly Dictionary<string, ObjectId[]> _previousSelectionMap;
        private ObjectId[] _baseIds;
        private ObjectId[] _currentIds;

        public FilterManagerService()
        {
            _filterRegistry = new Dictionary<string, IEntityFilter>();
            _previousSelectionMap = new Dictionary<string, ObjectId[]>();
            _baseIds = new ObjectId[0];
            _currentIds = new ObjectId[0];

            // 注册默认过滤器
            RegisterDefaultFilters();
        }

        /// <summary>
        /// 注册默认过滤器
        /// Register default filters
        /// </summary>
        private void RegisterDefaultFilters()
        {
            RegisterFilter(new LayerFilter());
            RegisterFilter(new ColorFilter());
            RegisterFilter(new LineTypeFilter());
            RegisterFilter(new LineWeightFilter());
            RegisterFilter(new TransparencyFilter());
            RegisterFilter(new TypeFilter());
        }

        /// <inheritdoc/>
        public void RegisterFilter(IEntityFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            if (string.IsNullOrWhiteSpace(filter.Key))
                throw new ArgumentException("Filter key cannot be null or empty", nameof(filter));

            if (!_filterRegistry.ContainsKey(filter.Key))
            {
                _filterRegistry[filter.Key] = filter;
            }
        }

        /// <inheritdoc/>
        public void ApplyFilter(string filterKey, bool isEnabled, Entity selectedEntity, ObjectId[] baseIds, out ObjectId[] currentIds)
        {
            if (selectedEntity == null)
            {
                currentIds = _currentIds;
                return;
            }

            if (!_filterRegistry.ContainsKey(filterKey))
            {
                currentIds = _currentIds;
                return;
            }

            // 应用过滤器获取结果
            var filterResult = _filterRegistry[filterKey].Apply(selectedEntity, baseIds);
            if (filterResult == null || filterResult.Length == 0)
            {
                currentIds = _currentIds;
                return;
            }

            // 初始化当前选择集
            if (_currentIds == null || _currentIds.Length == 0)
            {
                _currentIds = baseIds;
            }

            var current = _currentIds.AsEnumerable();

            if (isEnabled)
            {
                // 启用过滤器：保存当前状态并求交集
                if (!_previousSelectionMap.ContainsKey(filterKey))
                {
                    _previousSelectionMap[filterKey] = _currentIds.ToArray();
                }
                _currentIds = current.Intersect(filterResult).ToArray();
            }
            else
            {
                // 禁用过滤器：恢复之前的状态或求并集
                if (_previousSelectionMap.ContainsKey(filterKey))
                {
                    _currentIds = _previousSelectionMap[filterKey];
                    _previousSelectionMap.Remove(filterKey);
                }
                else
                {
                    _currentIds = current.Union(filterResult).ToArray();
                }
            }

            currentIds = _currentIds;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _currentIds = _baseIds;
            _previousSelectionMap.Clear();
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetRegisteredFilterKeys()
        {
            return _filterRegistry.Keys;
        }

        /// <inheritdoc/>
        public ObjectId[] GetCurrentSelection()
        {
            return _currentIds ?? new ObjectId[0];
        }

        /// <inheritdoc/>
        public void SetBaseSelection(ObjectId[] baseIds)
        {
            _baseIds = baseIds ?? new ObjectId[0];
            _currentIds = _baseIds;
            _previousSelectionMap.Clear();
        }
    }
}

