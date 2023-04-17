// The number of these two  directives must match the device's Tama Virtual Machine ID and Register Layout ID,
// respectively.
using Triamec.Tama.Vmid5;
using Triamec.Tama.Rlid19;
using System.Reflection;
using Triamec.TriaLink;
using System.Threading;

[assembly:AssemblyVersion("2.0.0.0")]

// The class name (as well as the project name and the namespace) contributes to the file name of the produced Tama
// program. This file is located in the bin\Debug or bin\Release subfolders and will commonly be copied into the
// Tama directory of the default workspace, too.
[Tama]
static class Messablauf {
	
	// Define constants for enhanced readability.
	static class State {
		public const int Idle = 0;
        public const int ReadParams = 1;
        public const int WorkingPositive = 2;
        public const int WorkingNegative = 3;
		public const int Wait = 99;
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
	static float _step_velocity;
	static float _step_size;


	// Choose how to run the program. Additional entry points for other tasks can be specified in this same program.
	[TamaTask(Task.IsochronousMain)]
	static void Main() {

		// Template state machine showing the picture of how a task is structured and which registers are commonly used
		// for status/control operations.
		switch (Register.Application.TamaControl.IsochronousMainState)
		{

			case State.Idle:
				if (Register.Application.TamaControl.IsochronousMainCommand != 0)
				{
					_counter = 0;
					Register.Application.TamaControl.IsochronousMainState = State.ReadParams;
				}
				break;

			case State.ReadParams:
				_wait = Register.Application.Parameters.Integers[0];
				_repeats = Register.Application.Parameters.Integers[1];
				_step_size = Register.Application.Parameters.Floats[0];
				_step_velocity = Register.Application.Parameters.Floats[1];

				if (Register.Application.TamaControl.IsochronousMainCommand == Command.MeasurePositive ||
                    Register.Application.TamaControl.IsochronousMainCommand == Command.MeasurePositiveAndNegative)
                {
					MoveTo(-365f);
                    _timer = _wait;
                    Register.Application.TamaControl.IsochronousMainState = State.WorkingPositive;
				}
				else if (Register.Application.TamaControl.IsochronousMainCommand == Command.MeasureNegative)
				{
					MoveTo(0f);
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
                        MoveStepAndWait(_step_size, _wait);
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
                        MoveStepAndWait(-_step_size, _wait);
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

	static void MoveStepAndWait(float step, int wait)
	{
        _timer = wait;
        _counter = _counter + 1;
        Register.Axes_0.Commands.PathPlanner.Xnew = step;
        Register.Axes_0.Commands.PathPlanner.Vnew = _step_velocity;
        Register.Axes_0.Commands.PathPlanner.Command = PathPlannerCommand.MoveRelative_Vel;
    }

    static void MoveTo(float x)
	{
        Register.Axes_0.Commands.PathPlanner.Xnew = x;
		Register.Axes_0.Commands.PathPlanner.Vnew = _step_velocity;
        Register.Axes_0.Commands.PathPlanner.Command = PathPlannerCommand.MoveAbsolute_Vel;
    }
}
