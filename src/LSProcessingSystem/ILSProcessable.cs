namespace LSUtils.Processing;

/// <summary>
/// Interface for entities that can be processed by the LSProcessing system.
/// Represents objects that can have processing contexts associated with them and can be initialized
/// with custom processing workflows. This interface enables entities to participate in the 
/// processing pipeline as contextual targets for process execution.
/// </summary>
/// <remarks>
/// <para><strong>Core Concept:</strong></para>
/// <para>ILSProcessable entities serve as targets for process execution, allowing the system to</para>
/// <para>apply different processing logic based on the specific entity being targeted. This enables</para>
/// <para>highly customizable and entity-aware processing workflows.</para>
/// 
/// <para><strong>Processing Context Association:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Entity-Specific Logic</strong>: Each processable entity can define its own processing behavior</description></item>
/// <item><description><strong>Context Registration</strong>: Processing trees are registered per entity type and instance</description></item>
/// <item><description><strong>Unique Identification</strong>: The ID serves as a key for context association and lookup</description></item>
/// <item><description><strong>Dynamic Configuration</strong>: Initialize method allows runtime workflow customization</description></item>
/// </list>
/// 
/// <para><strong>Integration with Processing System:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Target Resolution</strong>: Processes can specify which entity they target</description></item>
/// <item><description><strong>Context Merging</strong>: Entity contexts merge with global and process-specific contexts</description></item>
/// <item><description><strong>Hierarchical Processing</strong>: Support for complex nested processing scenarios</description></item>
/// <item><description><strong>State Management</strong>: Entities can maintain processing state across multiple process executions</description></item>
/// </list>
/// 
/// <para><strong>Implementation Guidance:</strong></para>
/// <list type="bullet">
/// <item><description><strong>ID Generation</strong>: Ensure ID is unique and persistent for the entity's lifetime</description></item>
/// <item><description><strong>Initialize Pattern</strong>: Call Initialize during entity setup or when processing logic changes</description></item>
/// <item><description><strong>Context Scope</strong>: Define processing contexts that are specific to the entity's responsibilities</description></item>
/// <item><description><strong>Error Handling</strong>: Handle initialization failures gracefully to maintain system stability</description></item>
/// </list>
/// </remarks>
public interface ILSProcessable {
    public const string ID_LABEL = "id";
    /// <summary>
    /// Unique identifier for this processable entity.
    /// Used by the processing system to associate contexts, lookup registered workflows,
    /// and maintain entity-specific processing state.
    /// </summary>
    public System.Guid ID { get; }
    
    /// <summary>
    /// Initialize this processable entity with a custom processing context.
    /// Allows usage of local processing workflows that will be executed
    /// when processes target this entity.
    /// </summary>
    /// <param name="treeBuilder">Optional tree builder delegate for defining entity-specific processing logic.</param>
    /// <param name="manager">Optional context manager to register the processing context with. Uses singleton if not provided.</param>
    /// <returns>The initialization status indicating success, failure, or other processing states.</returns>
    LSProcessResultStatus Initialize(LSProcessBuilderAction? treeBuilder = null, LSProcessManager? manager = null);
}
