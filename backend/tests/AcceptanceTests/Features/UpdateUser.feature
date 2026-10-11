Feature: Update a user

  Scenario: Updating a user replaces its details
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    When I update the user "Juste Leblanc" with
      | First name | Last name | Street         | Postal code | City      | Monthly quota |
      | Juste      | Leblond   | 8 quai du Port | 13002       | Marseille | 4             |
    Then the response status is 204
    When I get the user "Juste Leblanc"
    Then the user returned is
      | First name | Last name | Street         | Postal code | City      | Monthly quota | Status |
      | Juste      | Leblond   | 8 quai du Port | 13002       | Marseille | 4             | Active |

  Scenario: Updating an inactive user keeps it inactive
    Given the following users exist
      | First name | Last name | Status   |
      | Juste      | Leblanc   | Inactive |
    When I update the user "Juste Leblanc" with
      | First name | Last name | Street         | Postal code | City      | Monthly quota |
      | Juste      | Leblanc   | 8 quai du Port | 13002       | Marseille | 4             |
    And I get the user "Juste Leblanc"
    Then the user returned has the status "Inactive"

  Scenario: Updating rejects invalid details
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    When I update the user "Juste Leblanc" whose address.postalCode is "1300"
    Then the response is a problem with status 400

  Scenario: Updating an unknown user answers not found
    When I update an unknown user
    Then the response is a problem with status 404

  Scenario: Updating with the ETag read replaces the details
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    When I read the user "Juste Leblanc"
    And I update the user "Juste Leblanc" with the ETag read
    Then the response status is 204

  Scenario: Updating with an ETag read before another change is refused
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    When I read the user "Juste Leblanc"
    And I update the user "Juste Leblanc"
    And I update the user "Juste Leblanc" with the ETag read
    Then the response is a problem with status 412

  Scenario: Updating with an If-Match that is not an ETag of the API is rejected
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    When I update the user "Juste Leblanc" with the If-Match "not-an-etag"
    Then the response is a problem with status 400

  Scenario: A user stored with an address the current rules reject can still be corrected
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the user "Juste Leblanc" is stored with a blank address
    When I update the user "Juste Leblanc" with
      | First name | Last name | Street         | Postal code | City      | Monthly quota |
      | Juste      | Leblanc   | 8 quai du Port | 13002       | Marseille | 4             |
    Then the response status is 204
