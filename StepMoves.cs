// The number of these two  directives must match the device's Tama Virtual Machine ID and Register Layout ID,
// respectively.
using Triamec.Tama.Vmid5;
using Triamec.Tama.Rlid19;
using System.Reflection;
using Triamec.TriaLink;

[assembly:AssemblyVersion("2.0.0.0")]

// The class name (as well as the project name and the namespace) contributes to the file name of the produced Tama
// program. This file is located in the bin\Debug or bin\Release subfolders and will commonly be copied into the
// Tama directory of the default workspace, too.
[Tama]
static class StepMoves {
	
	// Define constants for enhanced readability.
	static class State {
		public const int Idle = 0;
        public const int WorkingPositive = 1;
        public const int WorkingNegative = 2;
		public const int Wait = 5;
	}
	static int _return_state;
    static class Command {
		public const int None = 0;
		public const int MeasurePositive = 1;
		public const int MeasureNegative = 2;
        public const int MeasurePositiveAndNegative = 3;
    }

	// Static variables can be used, but are not shared between the AsynchronousMain and the other tasks (Imagine a
	// [ThreadStatic] attribute here). For sharing, use general purpose registers.
	static int _counter;
	static int _wait; // in 10kHz counts, 10000 = 1s
    static int _timer; 
	static int _repeats;
	static float _stepVelocity;
	static float _stepSize;
	static float _startPositionPositive;
	static float _startPositionNegative;


	// Choose how to run the program. Additional entry points for other tasks can be specified in this same program.
	[TamaTask(Task.IsochronousMain)]
	static void Main() {

		// Template state machine showing the picture of how a task is structured and which registers are commonly used
		// for status/control operations.
		switch (Register.Application.TamaControl.IsochronousMainState)
		{

			case State.Idle:
				if (Register.Application.TamaControl.IsochronousMainCommand == Command.MeasurePositive ||
                    Register.Application.TamaControl.IsochronousMainCommand == Command.MeasurePositiveAndNegative)
                {
                    _counter = 0;
                    ReadParameters();
                    MoveTo(_startPositionPositive);
                    _timer = _wait;
                    Register.Application.TamaControl.IsochronousMainState = State.WorkingPositive;
				}
				else if (Register.Application.TamaControl.IsochronousMainCommand == Command.MeasureNegative)
				{
                    _counter = 0;
                    ReadParameters();
                    MoveTo(_startPositionNegative);
                    _timer = _wait;
                    Register.Application.TamaControl.IsochronousMainState = State.WorkingNegative;
				}
				break;


			case State.WorkingPositive:
				if (_timer == 0)
                {
					if (_counter == _repeats)
					{
						if (Register.Application.TamaControl.IsochronousMainCommand == Command.MeasurePositiveAndNegative)
						{
							_counter = 0;
							MoveTo(0f);
                            _timer = _wait;
                            Register.Application.TamaControl.IsochronousMainState = State.WorkingNegative;
                        }
                        else
						{
							Register.Application.TamaControl.IsochronousMainCommand = Command.None;
							Register.Application.TamaControl.IsochronousMainState = State.Idle;
						}
					}
					else
					{
                        MoveStepAndWait(_stepSize, _wait);
					}
                } 
				else if (Register.Axes_0.Signals.PathPlanner.Done)
				{
                    _return_state = State.WorkingPositive;
                    Register.Application.TamaControl.IsochronousMainState = State.Wait;
                }
				break;

			case State.WorkingNegative:
                if (_timer == 0)
                {
                    if (_counter == _repeats)
                    {
                        Register.Application.TamaControl.IsochronousMainCommand = Command.None;
                        Register.Application.TamaControl.IsochronousMainState = State.Idle;
                    }
                    else
                    {
                        MoveStepAndWait(-_stepSize, _wait);
                    }
                }
                else if (Register.Axes_0.Signals.PathPlanner.Done)
                {
                    _return_state = State.WorkingNegative;
                    Register.Application.TamaControl.IsochronousMainState = State.Wait;
                }
                break;

            case State.Wait:
				_timer = _timer - 1;
				if (_timer == 0)
				{
                    Register.Application.TamaControl.IsochronousMainState = _return_state;
                }
				break;

		}

		Register.Application.Variables.Integers[0] = _counter;
    }

	static void ReadParameters()
	{
        _wait = Register.Application.Parameters.Integers[0];
        _repeats = Register.Application.Parameters.Integers[1];
        _stepSize = Register.Application.Parameters.Floats[0];
        _stepVelocity = Register.Application.Parameters.Floats[1];
        _startPositionPositive = Register.Application.Parameters.Floats[2];
        _startPositionNegative = Register.Application.Parameters.Floats[3];
    }

	static void MoveStepAndWait(float step, int wait)
	{
        _timer = wait;
        _counter ++;
        Register.Axes_0.Commands.PathPlanner.Xnew = step;
        Register.Axes_0.Commands.PathPlanner.Vnew = _stepVelocity;
        Register.Axes_0.Commands.PathPlanner.Command = PathPlannerCommand.MoveRelative_Vel;
    }

    static void MoveTo(float x)
	{
        Register.Axes_0.Commands.PathPlanner.Xnew = x;
		Register.Axes_0.Commands.PathPlanner.Vnew = _stepVelocity;
        Register.Axes_0.Commands.PathPlanner.Command = PathPlannerCommand.MoveAbsolute_Vel;
    }
}
