Feature: API versioning

  Scenario: Responses report the supported API versions
    When I request "/v1/users"
    Then the response status is 200
    And the response header "api-supported-versions" is "1.0"

  Scenario: An unsupported API version is not found
    When I request "/v2/users"
    Then the response is a problem with status 404

  Scenario: Unversioned API routes are not found
    When I request "/users"
    Then the response is a problem with status 404

  Scenario: The health endpoint stays unversioned
    When I request "/health"
    Then the response status is 200
