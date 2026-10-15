class DomainError(Exception):
    """Violation d'une règle métier (invariant du domaine). Traduite en HTTP 400 par l'API."""
