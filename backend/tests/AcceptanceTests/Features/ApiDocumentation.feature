Feature: API documentation

  Scenario: The v1 OpenAPI document describes the users endpoint
    Given the API runs in the Development environment
    When I request "/openapi/v1.json"
    Then the response status is 200
    And the OpenAPI document lists the path "/v1/users"

  Scenario: The Scalar reference is served in Development
    Given the API runs in the Development environment
    When I request "/scalar/"
    Then the response status is 200

  Scenario: API documentation is not exposed outside Development
    When I request "/openapi/v1.json"
    Then the response is a problem with status 404
