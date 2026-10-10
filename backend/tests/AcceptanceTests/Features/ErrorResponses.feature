Feature: Error responses

  Scenario: An unknown route answers with a not found problem
    When I request "/does-not-exist"
    Then the response is a problem with status 404
