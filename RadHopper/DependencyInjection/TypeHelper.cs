namespace RadHopper.DependencyInjection;

internal class TypeHelper
{
    public static T ConstructType<T>(Type typeToCreate, IServiceProvider serviceProvider)
    {
        try
        {
            // Get the constructor that needs to be called
            var constructor = typeToCreate.GetConstructors().Single();

            // Resolve the parameters of the constructor from the DI container
            var parameterTypes = constructor.GetParameters()
                .Select(p => p.ParameterType)
                .ToArray();

            // For each parameter, get the corresponding service from the DI container
            var parameters = parameterTypes.Select(serviceProvider.GetService)
                .ToArray();

            // Check if any dependencies couldn't be resolved
            if (parameters.Contains(null))
                throw new TypeConstructorException(
                    $"Couldn't resolve all dependencies for {typeToCreate.FullName}");

            // Invoke the constructor to create the instance of T
            var instance = constructor.Invoke(parameters);

            // Optionally, you can call methods or interact with the instance
            if (instance is not T result)
                throw new TypeConstructorException($"Instance could not be cast!");

            return result;
        }
        catch (InvalidOperationException)
        {
            throw new TypeConstructorException(
                $"Invalid constructor for {typeToCreate.FullName}. Make sure there is only 1 constructor!");
        }
    }
}