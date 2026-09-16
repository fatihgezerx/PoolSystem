using System.Runtime.CompilerServices;

// Lets the Editor assembly assign Poolable.poolType directly (an internal field) after a
// PoolData "Compile" pass, without exposing a public runtime setter that gameplay code
// could accidentally call to reassign an object's pool type at runtime.
[assembly: InternalsVisibleTo("PoolSystem.Editor")]
