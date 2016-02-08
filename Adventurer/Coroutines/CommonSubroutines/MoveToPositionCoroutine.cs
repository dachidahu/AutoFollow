using System.Threading.Tasks;
using Adventurer.Game.Quests;
using Zeta.Common;
using Logger = Adventurer.Util.Logger;

namespace Adventurer.Coroutines.CommonSubroutines
{
    public class MoveToPositionCoroutine : ISubroutine
    {
        private readonly int _worldId;
        private readonly int _distance;
        private readonly Vector3 _position;
        private bool _isDone;
        private States _state;

        #region State

        public enum States
        {
            NotStarted,
            Moving,
            Completed,
            Failed
        }

        public States State
        {
            get { return _state; }
            protected set
            {
                if (_state == value) return;
                if (value != States.NotStarted)
                {
                    Logger.Debug("[MoveToPosition] " + value);
                }
                _state = value;
            }
        }

        #endregion

        public bool IsDone
        {
            get { return _isDone || AdvDia.CurrentWorldId != _worldId; }
        }

        public MoveToPositionCoroutine(int worldId, Vector3 position, int distance = 1)
        {
            _distance = distance;
            _worldId = worldId;
            _position = position;
        }

        public async Task<bool> GetCoroutine()
        {
            switch (State)
            {
                case States.NotStarted:
                    return NotStarted();
                case States.Moving:
                    return await Moving();
                case States.Completed:
                    return Completed();
                case States.Failed:
                    return Failed();
            }
            return false;
        }

        public void Reset()
        {
            _isDone = false;
            _state = States.NotStarted;
        }

        public void DisablePulse()
        {
        }

        public BountyData BountyData
        {
            get { return null; }
        }

        private bool NotStarted()
        {
            State = States.Moving;
            return false;
        }

        private async Task<bool> Moving()
        {
            if (!await NavigationCoroutine.MoveTo(_position, _distance)) return false;
            if (AdvDia.MyPosition.Distance(_position) > 20)
            {
                return false;
            }
            State = States.Completed;
            return false;
        }

        private bool Completed()
        {
            _isDone = true;
            return true;
        }

        private bool Failed()
        {
            _isDone = true;
            return true;
        }

    }
}
