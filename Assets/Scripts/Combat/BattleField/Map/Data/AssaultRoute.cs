using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    [Serializable]
    public sealed class AssaultRoute
    {
        [SerializeField] private string _routeId;
        [SerializeField] private string _displayName;
        [SerializeField] private List<Vector3> _corners = new();

        public string RouteId => _routeId;
        public string DisplayName => _displayName;
        public IReadOnlyList<Vector3> Corners => _corners;

        public AssaultRoute(string routeId, string displayName, IEnumerable<Vector3> corners)
        {
            _routeId = routeId ?? string.Empty;
            _displayName = displayName ?? string.Empty;
            _corners = corners != null ? new List<Vector3>(corners) : new List<Vector3>();
        }
    }
}
