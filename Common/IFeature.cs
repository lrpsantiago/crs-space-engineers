namespace IngameScript
{
	partial class Program
	{
		private interface IFeature : IUpdatable, ISetupable, IResetable
		{

		}

		private interface ISetupable
		{
			void Setup();
		}

		private interface IUpdatable
		{
			void Update(float delta);
		}

		private interface IResetable
		{
			void Reset();
		}
	}
}