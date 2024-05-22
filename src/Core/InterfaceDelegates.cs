namespace Kcky.GameNewt;

/// <summary>
/// Provides input for the local client, which is to be applied to the game simulation.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
/// <returns>Current input for the local client.</returns>
/// <remarks>
/// This is expected to gather recent keypresses, UI events, mouse movement etc.
/// </remarks>
public delegate TClientInput ProvideClientInputDelegate<TClientInput>();

/// <summary>
/// Provides server input for the server, which is to be applied to the game simulation.
/// </summary>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
/// <param name="state">Read only borrow of the latest authoritative game state (May not be modified by the input provider).</param>
/// <returns>Undeterministic input based on the state and other information.</returns>
/// <remarks>
/// This is the place to handle undeterministic events affecting the game state (e.g. loading client names from a database,
/// undeterministic physics simulation, loading server-side config files, adding randomness).
/// </remarks>
public delegate TServerInput ProvideServerInputDelegate<TServerInput, in TGameState>(TGameState state);

/// <summary>
/// Predicts input of a client for the next frame based on current client input.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
/// <param name="input">The current client input which is to be modified into the prediction.</param>
public delegate void PredictClientInputDelegate<TClientInput>(ref TClientInput input);

/// <summary>
/// Predicts server input for the next frame based on current server input and game state.
/// </summary>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
/// <param name="input">The current server input which is to be modified into the prediction.</param>
/// <param name="state">Read only borrow of the game state (may not be modified by the prediction).</param>
/// <remarks>
/// This is meant to be used for the client side prediction of server's behaviour (e.g. predict undeterministic physics events).
/// </remarks>
public delegate void PredictServerInputDelegate<TServerInput, in TGameState>(ref TServerInput input, TGameState state);

/// <summary>
/// Called when the client is initialized.
/// </summary>
/// <param name="id">The id of the client. May be used to determine, which data of the game state corresponds to the local client.</param>
public delegate void HandleClientInitializeDelegate(int id);

/// <summary>
/// Receives new authoritative state.
/// </summary>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
/// <param name="frame">The frame index (called in ascending continuous order).</param>
/// <param name="gameState">Read only borrow of the game state (May not be modified by the displayer).</param>
/// <remarks>
/// Mostly useful to display the game to the players. It is up to the game programmer to pick the best
/// representation of the two distinct states: the authoritative is final, but delayed, whereas the predict state is immediate
/// but slightly incorrect. Generally it is good to show what results from client inputs as predicted whereas global actions should be authorized
/// </remarks>
public delegate void HandleNewAuthoritativeStateDelegate<in TGameState>(long frame, TGameState gameState);

/// <summary>
/// Receives new predictive state.
/// </summary>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
/// <param name="frame">The frame index (called in ascending continuous order).</param>
/// <param name="gameState">Read only borrow of the game state (May not be modified by the displayer).</param>
/// <remarks>
/// Mostly useful to display the game to the players. It is up to the game programmer to pick the best
/// representation of the two distinct states: the authoritative is final, but delayed, whereas the predict state is immediate
/// but slightly incorrect. Generally it is good to show what results from client inputs as predicted whereas global actions should be authorized.
/// </remarks>
public delegate void HandleNewPredictiveStateDelegate<in TGameState>(long frame, TGameState gameState);

/// <summary>
/// Used to initialize the game state before the game begins.
/// May be undeterministic as it is called only for the server and the resulting state is replicated to clients.
/// </summary>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
/// <param name="state">Borrow of the game state to be modified.</param>
public delegate void InitializeStateDelegate<TGameState>(TGameState state);
