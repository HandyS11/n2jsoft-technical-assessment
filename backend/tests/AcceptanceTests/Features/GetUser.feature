Feature: Get a user

  Scenario: Getting a user returns all its details
    Given the following users exist
      | First name | Last name | Street           | Postal code | City  | Monthly quota | Status   |
      | Juste      | Leblanc   | 12 rue de Rivoli | 75001       | Paris | 5             | Inactive |
    When I get the user "Juste Leblanc"
    Then the response status is 200
    And the user returned is
      | First name | Last name | Street           | Postal code | City  | Monthly quota | Status   |
      | Juste      | Leblanc   | 12 rue de Rivoli | 75001       | Paris | 5             | Inactive |

  Scenario: Getting a user gives its version as ETag
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    When I get the user "Juste Leblanc"
    Then the response ETag is the version of the user returned

  Scenario: Getting an unknown user answers not found
    When I get an unknown user
    Then the response is a problem with status 404

  Scenario: Getting a user with a malformed id answers not found
    When I request "/v1/users/not-a-guid"
    Then the response is a problem with status 404

  Scenario: A deleted user cannot be read
    Given the following users exist
      | First name | Last name | Status  |
      | Juste      | Leblanc   | Deleted |
    When I get the user "Juste Leblanc"
    Then the response is a problem with status 404
